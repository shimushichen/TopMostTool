using System.Runtime.InteropServices;

namespace TopMostTool;

internal sealed class ScreenshotSelectionOverlay : Form
{
    private readonly Bitmap _desktop;
    private readonly Rectangle _screenBounds;
    private Point _start;
    private bool _dragging;
    private Point _pointer;

    internal Rectangle SelectedRegion { get; private set; }

    // The caller owns the desktop bitmap for the duration of the selection.
    internal ScreenshotSelectionOverlay(Bitmap desktop, Rectangle screenBounds)
    {
        _desktop = desktop;
        _screenBounds = screenBounds;
        Text = "窗口置顶 · 截图贴图";
        UiTheme.StyleForm(this);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        Bounds = screenBounds;
        _pointer = new Point(screenBounds.Width / 2, screenBounds.Height / 2);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Bounds = _screenBounds;
        Activate();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        // The capture and mouse coordinates must remain in physical desktop pixels.
        e.Cancel = true;
        base.OnDpiChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(_desktop, Point.Empty);
        using var shade = new SolidBrush(Color.FromArgb(112, 12, 18, 22));
        using var outside = new Region(ClientRectangle);
        if (!SelectedRegion.IsEmpty)
        {
            outside.Exclude(SelectedRegion);
        }
        e.Graphics.FillRegion(shade, outside);

        if (SelectedRegion.Width > 0 && SelectedRegion.Height > 0)
        {
            using var border = new Pen(UiTheme.Accent, UiTheme.Scale(2, DeviceDpi));
            e.Graphics.DrawRectangle(border, SelectedRegion);
            using var contrast = new Pen(Color.White);
            Rectangle inner = SelectedRegion;
            inner.Inflate(-1, -1);
            if (inner.Width > 0 && inner.Height > 0) e.Graphics.DrawRectangle(contrast, inner);
        }
        Rectangle monitor = Screen.FromPoint(PointToScreen(_pointer)).Bounds;
        monitor.Offset(-Left, -Top);
        Rectangle safe = Rectangle.Intersect(monitor, ClientRectangle);
        if (safe.IsEmpty) safe = ClientRectangle;
        int gap = UiTheme.Scale(16, DeviceDpi);
        if (_dragging && !SelectedRegion.IsEmpty)
        {
            string dimensions = $"{SelectedRegion.Width} × {SelectedRegion.Height} px · 松开生成贴图 · 右键或 Esc 取消";
            int hintHeight = Font.Height + UiTheme.Scale(24, DeviceDpi);
            int y = SelectedRegion.Bottom + gap;
            if (y + hintHeight > safe.Bottom) y = SelectedRegion.Top - hintHeight - gap;
            // Do not place instructions over selected pixels when there is no outside space.
            if (y >= safe.Top && y + hintHeight <= safe.Bottom)
                UiTheme.DrawOverlayHint(e.Graphics, dimensions, new Point(SelectedRegion.Left, y), safe, DeviceDpi);
        }
        else
        {
            string hint = "截图贴图 · 按住鼠标拖动框选\n松开生成贴图 · 右键或 Esc 取消";
            UiTheme.DrawOverlayHint(e.Graphics, hint, new Point(safe.Left + gap, safe.Top + gap), safe, DeviceDpi);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right)
        {
            CancelSelection();
        }
        else if (e.Button == MouseButtons.Left)
        {
            _start = e.Location;
            SelectedRegion = Rectangle.Empty;
            _dragging = true;
            Capture = true;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _pointer = e.Location;
        if (_dragging)
        {
            SelectedRegion = ScreenCapture.GetSelection(_start, e.Location, _desktop.Size);
            Invalidate();
        }
        else Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging || e.Button != MouseButtons.Left) return;

        SelectedRegion = ScreenCapture.GetSelection(_start, e.Location, _desktop.Size);
        _dragging = false;
        Capture = false;
        if (SelectedRegion.Width < 2 || SelectedRegion.Height < 2)
        {
            SelectedRegion = Rectangle.Empty;
            Invalidate();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (_dragging && !Capture) CancelSelection();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            CancelSelection();
        }
    }

    private void CancelSelection()
    {
        _dragging = false;
        Capture = false;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x0024 && m.LParam != IntPtr.Zero) // WM_GETMINMAXINFO
        {
            // Windows' default maximum form size can be smaller than a multi-monitor desktop.
            var info = Marshal.PtrToStructure<NativeMethods.MinMaxInfo>(m.LParam);
            info.MaxTrackSize = new NativeMethods.Point(
                Math.Max(info.MaxTrackSize.X, _screenBounds.Width),
                Math.Max(info.MaxTrackSize.Y, _screenBounds.Height));
            Marshal.StructureToPtr(info, m.LParam, false);
        }
    }
}
