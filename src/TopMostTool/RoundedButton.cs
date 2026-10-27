using System.Drawing.Drawing2D;

namespace TopMostTool;

// Keep Button's keyboard, dialog and accessibility behavior, but paint the
// complete rounded surface ourselves. Clipping a native rectangular border
// with a Region leaves disconnected corners and a second square focus frame.
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
        // All themed buttons sit on a solid surface. Paint the corners with
        // that surface instead of cutting off the antialiased rounded stroke.
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Background);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // ButtonBase can mark itself Opaque and skip OnPaintBackground,
        // especially for WM_PRINT/DrawToBitmap. Always initialize the corners.
        OnPaintBackground(e);
        if (Width < 2 || Height < 2) return;
        bool pressed = Enabled && ((_mousePressed && _hovered) || _keyPressed);
        Color background = !Enabled ? UiTheme.Background : pressed
            ? FlatAppearance.MouseDownBackColor : _hovered
            ? FlatAppearance.MouseOverBackColor : BackColor;
        if (background.IsEmpty) background = BackColor;
        Color foreground = Enabled ? ForeColor : UiTheme.DisabledText;
        Color borderColor = Enabled ? FlatAppearance.BorderColor : UiTheme.Border;
        float radius = Math.Min(UiTheme.Scale(8, DeviceDpi), (Math.Min(Width, Height) - 1) / 2F);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var fill = new SolidBrush(background))
            e.Graphics.FillRoundedRectangle(fill, 0.5F, 0.5F, Width - 1, Height - 1, radius);
        if (FlatAppearance.BorderSize > 0)
        {
            // One physical pixel, wholly inside the client area at every DPI.
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
        int gap = Image is not null && textSize.Width > 0 ? UiTheme.Scale(4, DeviceDpi) : 0;
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
            using var focus = new Pen(ForeColor == UiTheme.AccentText ? ForeColor : UiTheme.Accent)
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
