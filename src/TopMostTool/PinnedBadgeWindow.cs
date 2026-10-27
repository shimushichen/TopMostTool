using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TopMostTool;

internal sealed class PinnedBadgeWindow : Form
{
    private readonly IntPtr _targetHandle;
    private readonly Bitmap _image = TrayIconFactory.CreatePinImage(26);
    private readonly double _imageCenterY;
    private int _syncQueued;
    private bool _surfaceDirty = true;
    private bool _syncing;

    internal PinnedBadgeWindow(IntPtr targetHandle)
    {
        _targetHandle = targetHandle;
        // Center the visible pin, whose cap carries more weight than its stem.
        double alpha = 0, weightedY = 0;
        for (int y = 0; y < _image.Height; y++)
            for (int x = 0; x < _image.Width; x++)
            {
                byte opacity = _image.GetPixel(x, y).A;
                alpha += opacity;
                weightedY += (y + 0.5) * opacity;
            }
        _imageCenterY = alpha > 0 ? weightedY / alpha : _image.Height / 2.0;
        Text = "窗口已置顶";
        AccessibleName = "窗口已置顶";
        AutoScaleMode = AutoScaleMode.None;
        ControlBox = false;
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = _image.Size;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = Padding.Empty;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= unchecked((int)(NativeMethods.WsExTransparent |
                                                   NativeMethods.WsExToolWindow |
                                                   NativeMethods.WsExNoActivate |
                                                   NativeMethods.WsExLayered));
            return parameters;
        }
    }

    internal void Sync()
    {
        if (_syncing || IsDisposed) return;
        _syncing = true;
        try
        {
            if (!NativeMethods.IsWindow(_targetHandle) ||
                !NativeMethods.IsWindowVisible(_targetHandle) ||
                NativeMethods.IsIconic(_targetHandle) ||
                !NativeMethods.GetWindowRect(_targetHandle, out NativeMethods.Rect rect))
            {
                Hide();
                return;
            }

            if (!Visible)
            {
                Location = GetTargetLocation(rect);
                Show();
                _surfaceDirty = true;
            }

            // A layered window's backing bitmap and its native window must
            // share physical dimensions. Show/DPI changes can resize the form
            // after handle creation; restore and repaint once that has settled.
            if (ClientSize != _image.Size) ClientSize = _image.Size;
            Point location = GetTargetLocation(rect);
            if (_surfaceDirty)
            {
                RenderLayeredImage(location);
                _surfaceDirty = false;
            }

            NativeMethods.SetWindowPos(
                Handle,
                NativeMethods.HwndTopmost,
                location.X,
                location.Y,
                0,
                0,
                NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        }
        finally { _syncing = false; }
    }

    private Point GetTargetLocation(NativeMethods.Rect rect)
    {
        int dpi = DeviceDpi;
        // GetWindowRect includes invisible resize borders. A maximized window
        // therefore starts above its monitor (for example at -9 at 120 DPI).
        // Use the visible frame so the cap cannot disappear beyond the screen.
        NativeMethods.Rect frame = rect;
        if (NativeMethods.DwmGetWindowAttribute(_targetHandle, NativeMethods.DwmwaExtendedFrameBounds,
                out NativeMethods.Rect visibleFrame, Marshal.SizeOf<NativeMethods.Rect>()) == 0 &&
            visibleFrame.Right > visibleFrame.Left && visibleFrame.Bottom > visibleFrame.Top)
        {
            frame = visibleFrame;
        }

        int right = frame.Right - UiTheme.Scale(150, dpi);
        int captionTop = frame.Top, captionBottom = frame.Top;
        // Caption buttons grow with DPI and custom chrome can use other sizes.
        // Anchor to their actual boundary so a minimize glyph cannot show
        // through the pin's transparent area and make it look cut in half.
        if (NativeMethods.DwmGetWindowAttribute(_targetHandle, NativeMethods.DwmwaCaptionButtonBounds,
                out NativeMethods.Rect buttons, Marshal.SizeOf<NativeMethods.Rect>()) == 0 &&
            buttons.Left > 0 && buttons.Left < rect.Right - rect.Left &&
            buttons.Right > buttons.Left && buttons.Bottom > buttons.Top)
        {
            right = rect.Left + buttons.Left - UiTheme.Scale(6, dpi);
            captionTop = Math.Max(frame.Top, rect.Top + buttons.Top);
            captionBottom = Math.Min(frame.Bottom, rect.Top + buttons.Bottom);
        }
        // Windows Terminal exposes a separate drag-bar child for its custom
        // title bar. DWM still reports the shorter system caption there.
        IntPtr dragBar = NativeMethods.FindWindowEx(_targetHandle, IntPtr.Zero, "DRAG_BAR_WINDOW_CLASS", null);
        if (dragBar != IntPtr.Zero && NativeMethods.IsWindowVisible(dragBar) &&
            NativeMethods.GetWindowRect(dragBar, out NativeMethods.Rect bar) &&
            bar.Left <= right && bar.Right >= right && bar.Top >= frame.Top &&
            bar.Bottom <= frame.Bottom && bar.Bottom - bar.Top >= _image.Height &&
            bar.Bottom - bar.Top <= UiTheme.Scale(80, dpi))
        {
            captionTop = bar.Top;
            captionBottom = bar.Bottom;
        }
        int x = Math.Max(frame.Left + UiTheme.Scale(8, dpi), right - _image.Width);
        int y = frame.Top + 6;
        if (captionBottom > captionTop)
        {
            y = (int)Math.Round((captionTop + captionBottom) / 2.0 - _imageCenterY);
            y = Math.Clamp(y, captionTop, Math.Max(captionTop, captionBottom - _image.Height));
        }
        // A restored window can also be dragged partly off screen. Keep the
        // entire alpha canvas visible on the monitor nearest the badge.
        Rectangle screen = Screen.FromPoint(new Point(x + _image.Width / 2, y + _image.Height / 2)).Bounds;
        return new Point(
            Math.Clamp(x, screen.Left, Math.Max(screen.Left, screen.Right - _image.Width)),
            Math.Clamp(y, screen.Top, Math.Max(screen.Top, screen.Bottom - _image.Height)));
    }

    internal void QueueSync()
    {
        if (IsDisposed || !IsHandleCreated || Interlocked.Exchange(ref _syncQueued, 1) != 0)
        {
            return;
        }

        try
        {
            BeginInvoke((Action)(() =>
            {
                Interlocked.Exchange(ref _syncQueued, 0);
                if (!IsDisposed)
                {
                    Sync();
                }
            }));
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref _syncQueued, 0);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _surfaceDirty = true;
        QueueSync();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        _surfaceDirty = true;
        if (!_syncing) QueueSync();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Interlocked.Exchange(ref _syncQueued, 0);
        _surfaceDirty = true;
        base.OnHandleDestroyed(e);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        _surfaceDirty = true;
        QueueSync();
    }

    private void RenderLayeredImage(Point location)
    {
        // Per-pixel alpha preserves PNG edges without a color-key fringe.
        using Bitmap surface = CreateLayeredSurface(_image);

        IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) throw new Win32Exception();
        IntPtr memoryDc = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr previous = IntPtr.Zero;
        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero) throw new Win32Exception();
            bitmap = surface.GetHbitmap(Color.FromArgb(0));
            previous = NativeMethods.SelectObject(memoryDc, bitmap);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1)) throw new Win32Exception();
            var destination = new NativeMethods.Point(location.X, location.Y);
            var source = new NativeMethods.Point(0, 0);
            Size size = surface.Size;
            var blend = new NativeMethods.BlendFunction { SourceConstantAlpha = 255, AlphaFormat = 1 };
            if (!NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref destination, ref size,
                    memoryDc, ref source, 0, ref blend, NativeMethods.UlwAlpha))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1)) NativeMethods.SelectObject(memoryDc, previous);
            if (bitmap != IntPtr.Zero) NativeMethods.DeleteObject(bitmap);
            if (memoryDc != IntPtr.Zero) NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    internal static Bitmap CreateLayeredSurface(Bitmap image)
    {
        var surface = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
        using Graphics graphics = Graphics.FromImage(surface);
        graphics.PageUnit = GraphicsUnit.Pixel;
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        // The point overload of DrawImageUnscaled still uses image DPI. A
        // 96-DPI PNG on a 120-DPI bitmap is enlarged and clipped on the right
        // and bottom. Explicit source/destination pixel rectangles keep 1:1.
        var pixels = new Rectangle(Point.Empty, image.Size);
        graphics.DrawImage(image, pixels, pixels, GraphicsUnit.Pixel);
        return surface;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _image.Dispose();
        base.Dispose(disposing);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WmNcHitTest)
        {
            message.Result = (IntPtr)NativeMethods.HtTransparent;
            return;
        }

        base.WndProc(ref message);
    }
}
