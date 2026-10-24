// HecateHid.cs
// HECATE G3 MAX 2.4G 接收器（VID 35BB / PID A226）电量采集。
// 协议：向 col03 集合写 64 字节带编号报文 [D0 35 BB 00 ...]，
// 接收器约 10 ms 内回 [D0 35 BB 0A 00 01 level ...]，电量 = report[6]。
// 完整协议说明（含反例、边界与其它平台提示）见 docs/G3MAX_BATTERY_PROTOCOL.md。
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace NPS.HecateBattery;

internal sealed class HecateHid
{
    private const ushort TargetVid = 0x35BB;
    private const ushort TargetPid = 0xA226;
    private const int ReportLength = 64;
    private const int ReadWindowMs = 300;
    private const int ReadSliceMs = 250;
    private const int Attempts = 4;

    private static readonly Guid HidInterfaceGuid =
        new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");

    private static readonly string[] Collections = { "col03", "col02", "col04" };
    private static readonly byte[] CollectionRids = { 0xD0, 0x02, 0xED };

    private string? _cachedPath;
    private byte _cachedRid;

    /// <summary>成功返回 0..100；-1 = 设备不存在；-2 = 设备存在但无有效回包。</summary>
    public int Poll(out bool devicePresent)
    {
        devicePresent = false;

        if (_cachedPath != null && TryPoll(_cachedPath, _cachedRid, out int cachedLevel))
        {
            devicePresent = true;
            return cachedLevel;
        }
        _cachedPath = null;

        List<string> paths = EnumeratePaths();
        if (paths.Count == 0) return -1;

        foreach (string path in paths)
        {
            if (!MatchesDevice(path)) continue;

            devicePresent = true;
            byte rid = RidForPath(path);
            if (TryPoll(path, rid, out int level))
            {
                _cachedPath = path;
                _cachedRid = rid;
                return level;
            }
        }

        return -2;
    }

    private static bool TryPoll(string path, byte rid, out int level)
    {
        level = 0;

        // 读（overlapped）与写（同步）各开一个句柄；每次轮询结束都关闭，不长期占用设备。
        IntPtr readHandle = Native.CreateFileW(
            path,
            Native.GENERIC_READ | Native.GENERIC_WRITE,
            Native.FILE_SHARE_READ_WRITE,
            IntPtr.Zero,
            Native.OPEN_EXISTING,
            Native.FILE_FLAG_OVERLAPPED,
            IntPtr.Zero);

        if (readHandle == new IntPtr(-1)) return false;

        IntPtr writeHandle = Native.CreateFileW(
            path,
            Native.GENERIC_READ | Native.GENERIC_WRITE,
            Native.FILE_SHARE_READ_WRITE,
            IntPtr.Zero,
            Native.OPEN_EXISTING,
            Native.FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        if (writeHandle == new IntPtr(-1))
        {
            Native.CloseHandle(readHandle);
            return false;
        }

        IntPtr readEvent = Native.CreateEventW(IntPtr.Zero, true, false, null);
        if (readEvent == IntPtr.Zero)
        {
            Native.CloseHandle(readHandle);
            Native.CloseHandle(writeHandle);
            return false;
        }

        try
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                if (attempt > 0) Thread.Sleep(100);

                var query = new byte[ReportLength];
                query[0] = rid;
                query[1] = 0x35;
                query[2] = 0xBB;

                if (!Native.WriteFile(
                        writeHandle, query, ReportLength, out uint written, IntPtr.Zero) ||
                    written != ReportLength)
                {
                    continue;
                }

                long deadline = Environment.TickCount64 + ReadWindowMs;
                while (Environment.TickCount64 < deadline)
                {
                    int remain = (int)(deadline - Environment.TickCount64);
                    if (remain <= 0) break;

                    var report = new byte[ReportLength];
                    int got = ReadReport(readHandle, readEvent, report, Math.Min(remain, ReadSliceMs));
                    if (got <= 0) continue;

                    // 严格校验：RID 回显 + 魔法 35 BB + op 0x0A + 电量 0..100。
                    if (report[0] != rid || report[1] != 0x35 || report[2] != 0xBB) continue;
                    if (report[3] != 0x0A) continue;

                    int value = report[6];
                    if (value > 100) continue;

                    level = value;
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Native.CloseHandle(readEvent);
            Native.CloseHandle(readHandle);
            Native.CloseHandle(writeHandle);
        }
    }

    private static int ReadReport(IntPtr handle, IntPtr readEvent, byte[] buffer, int timeoutMs)
    {
        var overlapped = new Native.OVERLAPPED { hEvent = readEvent };
        Native.ResetEvent(readEvent);

        if (Native.ReadFile(handle, buffer, (uint)buffer.Length, out uint got, ref overlapped))
            return (int)got;

        int error = Marshal.GetLastWin32Error();
        if (error != 997) return -1; // ERROR_IO_PENDING

        int wait = Native.WaitForSingleObject(readEvent, (uint)timeoutMs);
        if (wait != 0)
        {
            // CancelIo 只取消「调用线程发起的 I/O」；本实现读与写在同一线程，安全。
            // 若以后改成跨线程读，请换成 CancelIoEx。
            Native.CancelIo(handle);
            Native.GetOverlappedResult(handle, ref overlapped, out _, true);
            return 0;
        }

        if (!Native.GetOverlappedResult(handle, ref overlapped, out got, false))
            return -1;

        return (int)got;
    }

    private static List<string> EnumeratePaths()
    {
        var found = new List<string>();
        Guid guid = HidInterfaceGuid;

        IntPtr set = Native.SetupDiGetClassDevsW(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (set == new IntPtr(-1)) return found;

        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new Native.SP_DEVICE_INTERFACE_DATA();
                data.cbSize = (uint)Marshal.SizeOf(typeof(Native.SP_DEVICE_INTERFACE_DATA));

                if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref data))
                    break;

                Native.SetupDiGetDeviceInterfaceDetailW(
                    set, ref data, IntPtr.Zero, 0, out uint required, IntPtr.Zero);

                if (required == 0) continue;

                IntPtr buffer = Marshal.AllocHGlobal((int)required + 8);
                try
                {
                    // SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize: 8 on x64, 6 on x86.
                    Marshal.WriteInt32(buffer, 0, IntPtr.Size == 8 ? 8 : 6);

                    if (!Native.SetupDiGetDeviceInterfaceDetailW(
                            set, ref data, buffer, required, out _, IntPtr.Zero))
                    {
                        continue;
                    }

                    string? path = ExtractWidePath(buffer, (int)required);
                    if (path == null) continue;

                    if (path.IndexOf("vid_35bb&pid_a226", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (!IsVendorCollection(path)) continue;
                    if (!found.Contains(path)) found.Add(path);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(set);
        }

        found.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
        return found;
    }

    private static string? ExtractWidePath(IntPtr buffer, int size)
    {
        // 扫描 UTF-16 的 "\\?\"（5C 00 5C 00 3F 00 5C 00），避免依赖结构体填充布局。
        for (int offset = 0; offset + 8 <= size; offset += 2)
        {
            if (Marshal.ReadByte(buffer, offset) == 0x5C &&
                Marshal.ReadByte(buffer, offset + 1) == 0x00 &&
                Marshal.ReadByte(buffer, offset + 2) == 0x5C &&
                Marshal.ReadByte(buffer, offset + 3) == 0x00 &&
                Marshal.ReadByte(buffer, offset + 4) == 0x3F &&
                Marshal.ReadByte(buffer, offset + 5) == 0x00 &&
                Marshal.ReadByte(buffer, offset + 6) == 0x5C &&
                Marshal.ReadByte(buffer, offset + 7) == 0x00)
            {
                return Marshal.PtrToStringUni(buffer + offset);
            }
        }

        return null;
    }

    private static bool MatchesDevice(string path)
    {
        IntPtr handle = Native.CreateFileW(
            path, 0, Native.FILE_SHARE_READ_WRITE,
            IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);

        if (handle == new IntPtr(-1)) return false;

        try
        {
            var attributes = new Native.HIDD_ATTRIBUTES();
            attributes.Size = (uint)Marshal.SizeOf(typeof(Native.HIDD_ATTRIBUTES));

            if (!Native.HidD_GetAttributes(handle, ref attributes)) return false;

            return attributes.VendorID == TargetVid && attributes.ProductID == TargetPid;
        }
        finally
        {
            Native.CloseHandle(handle);
        }
    }

    private static bool IsVendorCollection(string path)
    {
        for (int i = 0; i < Collections.Length; i++)
        {
            if (path.IndexOf(Collections[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static byte RidForPath(string path)
    {
        for (int i = 0; i < Collections.Length; i++)
        {
            if (path.IndexOf(Collections[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return CollectionRids[i];
        }

        return 0xD0;
    }

    private static int Rank(string path)
    {
        for (int i = 0; i < Collections.Length; i++)
        {
            if (path.IndexOf(Collections[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return i;
        }

        return 99;
    }
}
