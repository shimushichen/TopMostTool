using System.Drawing.Drawing2D;

namespace TopMostTool;

// Keep Button's keyboard, dialog and accessibility behavior while painting
// a crisp, Apple-minimalist rounded surface without clipping regions.
internal sealed class RoundedButton : Button
{
    private bool _hovered;
    private bool _mousePressed;
    private bool _keyPressed;

    internal RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Color cornerSurface = FlatAppearance.BorderSize == 0 && !BackColor.IsEmpty
            ? BackColor
            : Parent?.BackColor ?? UiTheme.Background;
        e.Graphics.Clear(cornerSurface);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        OnPaintBackground(e);
        if (Width < 2 || Height < 2) return;
        bool pressed = Enabled && ((_mousePressed && _hovered) || _keyPressed);
        bool isPrimary = BackColor == UiTheme.Accent;
        Color background = !Enabled ? UiTheme.SurfaceMuted : pressed
            ? FlatAppearance.MouseDownBackColor : _hovered
            ? FlatAppearance.MouseOverBackColor : BackColor;
        if (background.IsEmpty) background = BackColor;
        Color foreground = Enabled ? ForeColor : UiTheme.DisabledText;
        Color borderColor = !Enabled ? UiTheme.Border : isPrimary
            ? pressed ? UiTheme.AccentPressed : _hovered ? UiTheme.AccentHover : UiTheme.Accent
            : _hovered && ForeColor != UiTheme.Error ? Color.FromArgb(190, 190, 196)
            : FlatAppearance.BorderColor;
        float radius = Math.Min(UiTheme.Scale(7, DeviceDpi), (Math.Min(Width, Height) - 1) / 2F);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        bool hasBorder = FlatAppearance.BorderSize > 0;
        float fillInset = hasBorder ? 0.5F : 0F;
        using (var fill = new SolidBrush(background))
            e.Graphics.FillRoundedRectangle(fill, fillInset, fillInset, Width - fillInset * 2, Height - fillInset * 2, radius);

        if (hasBorder)
        {
            using var border = new Pen(borderColor);
            e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, Width - 1, Height - 1, radius);
        }

        Rectangle content = new(Padding.Left, Padding.Top,
            Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
        const TextFormatFlags measureFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        TextFormatFlags flags = measureFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter;
        if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
        else if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
        Size textSize = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(e.Graphics, Text, Font,
            Size.Empty, flags);
        int gap = Image is not null && textSize.Width > 0 ? UiTheme.Scale(5, DeviceDpi) : 0;
        int imageWidth = Image?.Width ?? 0;
        int groupWidth = imageWidth + gap + textSize.Width;
        int left = content.Left + (content.Width - groupWidth) / 2;
        if (Image is not null)
        {
            Point location = new(left, content.Top + (content.Height - Image.Height) / 2);
            if (Enabled) e.Graphics.DrawImageUnscaled(Image, location);
            else ControlPaint.DrawImageDisabled(e.Graphics, Image, location.X, location.Y, background);
        }
        if (textSize.Width > 0)
            TextRenderer.DrawText(e.Graphics, Text, Font,
                new Rectangle(left + imageWidth + gap, content.Top, textSize.Width, content.Height), foreground, flags);

        if (Focused && ShowFocusCues)
        {
            int inset = UiTheme.Scale(3, DeviceDpi);
            using var focus = new Pen(ForeColor == UiTheme.AccentText ? Color.FromArgb(180, 255, 255, 255) : UiTheme.Text)
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
    protected override void OnEnabledChanged(EventArgs e)
    {
        _mousePressed = _keyPressed = false;
        base.OnEnabledChanged(e); Invalidate();
    }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Invalidate(); }
}
