using System.Runtime.InteropServices;

namespace TopMostTool;

internal sealed class WindowSelectionOverlay : Form
{
    internal event EventHandler<IntPtr>? WindowSelected;
    internal event EventHandler? SelectionCancelled;
    private Bitmap? _desktop;
    private Rectangle _candidate;
    private Point _pointer;
    private readonly WindowManager _eligibility = new();

    internal WindowSelectionOverlay()
    {
        Text = "窗口置顶 · 选择窗口";
        UiTheme.StyleForm(this);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        TopMost = true;
        BackColor = Color.Black;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
    }

    internal void BeginSelection()
    {
        Bounds = SystemInformation.VirtualScreen;
        _desktop?.Dispose();
        _desktop = ScreenCapture.Capture(Bounds);
        _candidate = Rectangle.Empty;
        _pointer = PointToClient(Cursor.Position);
        Cursor = Cursors.Cross;
        Show();
        Activate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_desktop is not null) e.Graphics.DrawImageUnscaled(_desktop, Point.Empty);
        using var shade = new SolidBrush(Color.FromArgb(72, 12, 18, 22));
        using var outside = new Region(ClientRectangle);
        if (!_candidate.IsEmpty) outside.Exclude(_candidate);
        e.Graphics.FillRegion(shade, outside);
        if (!_candidate.IsEmpty)
        {
            using var border = new Pen(UiTheme.Accent, UiTheme.Scale(3, DeviceDpi));
            e.Graphics.DrawRectangle(border, _candidate);
        }
        Rectangle safe = Screen.FromPoint(PointToScreen(_pointer)).Bounds;
        safe.Offset(-Left, -Top);
        safe.Intersect(ClientRectangle);
        if (safe.IsEmpty) safe = ClientRectangle;
        int gap = UiTheme.Scale(16, DeviceDpi);
        UiTheme.DrawOverlayHint(e.Graphics, "窗口置顶 · 点击要置顶的应用窗口\n右键或 Esc 取消", new Point(safe.Left + gap, safe.Top + gap), safe, DeviceDpi);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _pointer = e.Location;
        Point screenPoint = PointToScreen(e.Location);
        Rectangle candidate = Rectangle.Empty;
        NativeMethods.EnumWindows((window, _) =>
        {
            if (!_eligibility.IsEligibleWindow(window, Handle) || !NativeMethods.GetWindowRect(window, out NativeMethods.Rect rect)) return true;
            Rectangle bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (!bounds.Contains(screenPoint)) return true;
            bounds.Offset(-Left, -Top);
            candidate = Rectangle.Intersect(bounds, ClientRectangle);
            return false;
        }, IntPtr.Zero);
        _candidate = candidate;
        Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) { _desktop?.Dispose(); _desktop = null; }
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        e.Cancel = true;
        base.OnDpiChanged(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right) { CancelSelection(); return; }
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Point screenPoint = PointToScreen(e.Location);
        Hide();
        Cursor = Cursors.Default;
        BeginInvoke((Action)(() =>
        {
            IntPtr target = NativeMethods.WindowFromPoint(new NativeMethods.Point(screenPoint.X, screenPoint.Y));
            target = target == IntPtr.Zero ? IntPtr.Zero : NativeMethods.GetAncestor(target, NativeMethods.GaRoot);
            WindowSelected?.Invoke(this, target);
        }));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            CancelSelection();
            e.SuppressKeyPress = true;
        }
    }

    private void CancelSelection()
    {
        Hide();
        Cursor = Cursors.Default;
        SelectionCancelled?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _desktop?.Dispose();
        base.Dispose(disposing);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x0024 && m.LParam != IntPtr.Zero)
        {
            Rectangle desktop = SystemInformation.VirtualScreen;
            var info = Marshal.PtrToStructure<NativeMethods.MinMaxInfo>(m.LParam);
            info.MaxTrackSize = new NativeMethods.Point(Math.Max(info.MaxTrackSize.X, desktop.Width), Math.Max(info.MaxTrackSize.Y, desktop.Height));
            Marshal.StructureToPtr(info, m.LParam, false);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            Cursor = Cursors.Default;
        }

        base.OnFormClosing(e);
    }
}
