using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace NPS.HecateBattery;

internal sealed class HecateDeviceNotifications : IDisposable
{
    private readonly Action _onChanged;
    private readonly Native.CM_NOTIFY_CALLBACK _callback;
    private IntPtr _registration;
    private int _disposed;

    public HecateDeviceNotifications(Action onChanged)
    {
        _onChanged = onChanged;
        _callback = OnNotification;

        var filter = new Native.CM_NOTIFY_FILTER
        {
            cbSize = (uint)Marshal.SizeOf<Native.CM_NOTIFY_FILTER>(),
            FilterType = Native.CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE,
            ClassGuid = HecateHid.HidInterfaceGuid,
        };
        uint result = Native.CM_Register_Notification(
            ref filter, IntPtr.Zero, _callback, out _registration);
        if (result != 0)
            throw new InvalidOperationException("CM_Register_Notification failed: " + result);
    }

    private uint OnNotification(
        IntPtr notification, IntPtr context, uint action, IntPtr eventData, uint eventDataSize)
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            (action != Native.CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL &&
             action != Native.CM_NOTIFY_ACTION_DEVICEINTERFACEREMOVAL) ||
            eventData == IntPtr.Zero || eventDataSize < 26 || eventDataSize > 4096)
            return 0;

        try
        {
            // CM_NOTIFY_EVENT_DATA.u.DeviceInterface.SymbolicLink starts after type, reserved and GUID.
            string? path = Marshal.PtrToStringUni(
                IntPtr.Add(eventData, 24), ((int)eventDataSize - 24) / 2);
            if (path != null &&
                path.Contains("vid_35bb&pid_a226", StringComparison.OrdinalIgnoreCase) &&
                path.Contains("col03", StringComparison.OrdinalIgnoreCase))
                _onChanged();
        }
        catch { }

        return 0;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (_registration != IntPtr.Zero)
            Native.CM_Unregister_Notification(_registration);
        _registration = IntPtr.Zero;
    }
}
