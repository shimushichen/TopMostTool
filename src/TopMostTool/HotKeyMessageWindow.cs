using System.Runtime.InteropServices;

namespace TopMostTool;

internal sealed class HotKeyMessageWindow : NativeWindow, IDisposable
{
    internal event EventHandler<int>? HotKeyPressed;

    internal HotKeyMessageWindow()
    {
        CreateHandle(new CreateParams
        {
            Caption = "TopMostToolHotKey",
            ExStyle = 0,
            Style = 0
        });
    }

    internal IntPtr WindowHandle => Handle;

    internal bool TryRegister(int id, HotKeyBinding binding, out int errorCode)
    {
        bool registered = NativeMethods.RegisterHotKey(
            Handle,
            id,
            binding.Modifiers | NativeMethods.HotKeyModifiers.NoRepeat,
            binding.Key);
        errorCode = registered ? 0 : Marshal.GetLastWin32Error();
        return registered;
    }

    internal void Unregister(int id)
    {
        if (Handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(Handle, id);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotKey)
        {
            HotKeyPressed?.Invoke(this, m.WParam.ToInt32());
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        DestroyHandle();
        GC.SuppressFinalize(this);
    }
}
