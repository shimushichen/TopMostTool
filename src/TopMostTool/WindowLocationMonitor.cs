namespace TopMostTool;

internal sealed class WindowLocationMonitor : IDisposable
{
    private readonly NativeMethods.WinEventProc _callback;
    private IntPtr _hook;
    private bool _disposed;

    internal event EventHandler<IntPtr>? WindowLocationChanged;

    internal WindowLocationMonitor()
    {
        _callback = OnWinEvent;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EventObjectLocationChange,
            NativeMethods.EventObjectLocationChange,
            IntPtr.Zero,
            _callback,
            0,
            0,
            NativeMethods.WineventOutOfContext | NativeMethods.WineventSkipOwnProcess);
    }

    internal bool IsAvailable => _hook != IntPtr.Zero;

    private void OnWinEvent(
        IntPtr hook,
        uint eventType,
        IntPtr hWnd,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (!_disposed && objectId == NativeMethods.ObjIdWindow && childId == 0 && hWnd != IntPtr.Zero)
        {
            WindowLocationChanged?.Invoke(this, hWnd);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
