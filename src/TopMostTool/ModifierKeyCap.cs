using System.Drawing.Drawing2D;

namespace TopMostTool;

// Minimalist monochrome hardware keycap styled after macOS Settings / Magic Keyboard.
internal sealed class ModifierKeyCap : CheckBox
{
    private bool _hovered;
    private bool _mousePressed;
    private bool _keyPressed;

    internal ModifierKeyCap()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int dpi = DeviceDpi;
        Size text = TextRenderer.MeasureText(Text, Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        return new Size(Math.Max(UiTheme.Scale(62, dpi),
                text.Width + Padding.Horizontal + UiTheme.Scale(20, dpi)),
            Math.Max(UiTheme.Scale(30, dpi), text.Height + Padding.Vertical + UiTheme.Scale(4, dpi)));
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Surface);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        OnPaintBackground(e);
        if (Width < 2 || Height < 2) return;
        int dpi = DeviceDpi;
        bool pressed = Enabled && ((_mousePressed && _hovered) || _keyPressed);
        Color fillColor = !Enabled ? UiTheme.SurfaceMuted
            : pressed ? UiTheme.AccentPressed
            : Checked ? UiTheme.Accent
            : _hovered ? UiTheme.Hover : Color.White;
        Color borderColor = !Enabled ? UiTheme.BorderSubtle
            : Checked ? UiTheme.Accent
            : _hovered ? Color.FromArgb(180, 180, 186) : UiTheme.Border;
        Color foreground = !Enabled ? UiTheme.DisabledText
            : Checked ? UiTheme.AccentText
            : UiTheme.Text;
        float radius = Math.Min(UiTheme.Scale(6, dpi), (Math.Min(Width, Height) - 1) / 2F);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using (var fill = new SolidBrush(fillColor))
            e.Graphics.FillRoundedRectangle(fill, 0.5F, 0.5F, Width - 1, Height - 1, radius);

        // Subtle 1px keycap base shadow when idle and unchecked
        if (Enabled && !Checked && !pressed && Height > 14)
        {
            using var lipPen = new Pen(Color.FromArgb(20, 0, 0, 0), 1F);
            e.Graphics.DrawLine(lipPen, radius, Height - 1.5F, Width - radius, Height - 1.5F);
        }

        using (var border = new Pen(borderColor))
            e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, Width - 1, Height - 1, radius);

        Rectangle textBounds = new(Padding.Left, Padding.Top,
            Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
        {
            int inset = UiTheme.Scale(3, dpi);
            using var focus = new Pen(Checked ? Color.FromArgb(180, 255, 255, 255) : UiTheme.Text)
                { DashStyle = DashStyle.Dot };
            e.Graphics.DrawRoundedRectangle(focus, inset + 0.5F, inset + 0.5F,
                Width - inset * 2 - 1, Height - inset * 2 - 1, Math.Max(1, radius - inset));
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; base.OnMouseLeave(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) _mousePressed = true;
        base.OnMouseDown(e); Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e) { _mousePressed = false; base.OnMouseUp(e); Invalidate(); }
    protected override void OnMouseCaptureChanged(EventArgs e) { _mousePressed = false; base.OnMouseCaptureChanged(e); Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) _keyPressed = true; base.OnKeyDown(e); Invalidate(); }
    protected override void OnKeyUp(KeyEventArgs e) { _keyPressed = false; base.OnKeyUp(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { _keyPressed = false; base.OnLostFocus(e); Invalidate(); }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e)
    {
        _mousePressed = _keyPressed = false;
        base.OnEnabledChanged(e); Invalidate();
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Invalidate(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Invalidate(); }
}
