using System.Drawing.Drawing2D;
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
        e.Cancel = true;
        base.OnDpiChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(_desktop, Point.Empty);
        using var shade = new SolidBrush(Color.FromArgb(108, 16, 16, 18));
        using var outside = new Region(ClientRectangle);
        if (!SelectedRegion.IsEmpty)
        {
            outside.Exclude(SelectedRegion);
        }
        e.Graphics.FillRegion(shade, outside);

        if (SelectedRegion.Width > 0 && SelectedRegion.Height > 0)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            // Pure Apple-minimalist selection outline: crisp white border + subtle outer contrast line.
            Rectangle outer = SelectedRegion;
            outer.Inflate(1, 1);
            using (var outerContrast = new Pen(Color.FromArgb(140, 0, 0, 0), 1F))
                e.Graphics.DrawRectangle(outerContrast, outer);
            using (var border = new Pen(Color.White, UiTheme.Scale(1, DeviceDpi)))
                e.Graphics.DrawRectangle(border, SelectedRegion);
            DrawSelectionHandles(e.Graphics);
        }
        Rectangle monitor = Screen.FromPoint(PointToScreen(_pointer)).Bounds;
        monitor.Offset(-Left, -Top);
        Rectangle safe = Rectangle.Intersect(monitor, ClientRectangle);
        if (safe.IsEmpty) safe = ClientRectangle;
        int gap = UiTheme.Scale(14, DeviceDpi);
        if (_dragging && !SelectedRegion.IsEmpty)
        {
            string dimensions = $"{SelectedRegion.Width} × {SelectedRegion.Height}  ·  松开生成贴图  ·  Esc 取消";
            int hintHeight = Font.Height + UiTheme.Scale(22, DeviceDpi);
            int y = SelectedRegion.Bottom + gap;
            if (y + hintHeight > safe.Bottom) y = SelectedRegion.Top - hintHeight - gap;
            if (y >= safe.Top && y + hintHeight <= safe.Bottom)
                UiTheme.DrawOverlayHint(e.Graphics, dimensions, new Point(SelectedRegion.Left, y), safe, DeviceDpi);
        }
        else
        {
            string hint = "拖动框选区域生成贴图  ·  右键或 Esc 取消";
            UiTheme.DrawOverlayHint(e.Graphics, hint, new Point(safe.Left + gap, safe.Top + gap), safe, DeviceDpi);
        }
    }

    private void DrawSelectionHandles(Graphics graphics)
    {
        if (SelectedRegion.Width < 16 || SelectedRegion.Height < 16) return;
        int size = Math.Max(6, UiTheme.Scale(7, DeviceDpi));
        float offset = size / 2F;
        Rectangle region = SelectedRegion;
        Point[] centers =
        {
            new(region.Left, region.Top), new(region.Left + region.Width / 2, region.Top), new(region.Right, region.Top),
            new(region.Left, region.Top + region.Height / 2), new(region.Right, region.Top + region.Height / 2),
            new(region.Left, region.Bottom), new(region.Left + region.Width / 2, region.Bottom), new(region.Right, region.Bottom)
        };
        using var fill = new SolidBrush(Color.White);
        using var border = new Pen(Color.FromArgb(180, 29, 29, 31), 1.2F);
        foreach (Point center in centers)
        {
            var bounds = new RectangleF(center.X - offset, center.Y - offset, size, size);
            graphics.FillEllipse(fill, bounds);
            graphics.DrawEllipse(border, bounds);
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
        if (m.Msg == 0x0024 && m.LParam != IntPtr.Zero)
        {
            var info = Marshal.PtrToStructure<NativeMethods.MinMaxInfo>(m.LParam);
            info.MaxTrackSize = new NativeMethods.Point(
                Math.Max(info.MaxTrackSize.X, _screenBounds.Width),
                Math.Max(info.MaxTrackSize.Y, _screenBounds.Height));
            Marshal.StructureToPtr(info, m.LParam, false);
        }
    }
}
