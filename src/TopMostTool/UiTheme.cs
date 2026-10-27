namespace TopMostTool;

internal static class UiTheme
{
    // One stable light palette. High contrast uses the user's system colors.
    internal static Color Background => SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(246, 247, 248);
    internal static Color Surface => SystemInformation.HighContrast ? SystemColors.Window : Color.White;
    internal static Color Text => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(32, 38, 43);
    internal static Color MutedText => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(91, 101, 110);
    internal static Color DisabledText => SystemInformation.HighContrast ? SystemColors.GrayText : Color.FromArgb(128, 135, 142);
    internal static Color Border => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(218, 224, 228);
    internal static Color Accent => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(0, 112, 110);
    internal static Color AccentText => SystemInformation.HighContrast ? SystemColors.HighlightText : Color.White;
    internal static Color Hover => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(228, 241, 239);
    internal static Color Pressed => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(207, 229, 225);
    internal static Color Error => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(174, 37, 47);
    internal static Color DangerHover => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(253, 229, 230);
    internal static readonly Font MenuFont = new(SystemFonts.MessageBoxFont!.FontFamily, Math.Max(9F, SystemFonts.MessageBoxFont.Size));
    internal static readonly Font EmphasisFont = new(MenuFont, FontStyle.Bold);
    internal static readonly Font HeadingFont = new(MenuFont.FontFamily, MenuFont.Size + 7F, FontStyle.Bold);
    internal const int IconSize = 20;
    internal const int ButtonHeight = 36;
    internal const int Spacing = 8;
    internal const int PagePadding = 24;
    internal const int CornerRadius = 10;

    internal static int Scale(int value, int dpi) => Math.Max(1, (int)Math.Round(value * dpi / 96.0));

    internal static void StyleMenu(ToolStripDropDownMenu menu)
    {
        menu.Renderer = new CenteredMenuRenderer(new MenuColorTable());
        menu.BackColor = Surface;
        menu.ForeColor = Text;
        menu.ShowItemToolTips = true;
        menu.AutoSize = true;
        menu.Font = MenuFont;
        menu.Padding = new Padding(3);
        ApplyRoundedCorners(menu, CornerRadius);
        menu.Opening += (_, _) => RefreshMenuMetrics(menu);
        menu.DpiChangedAfterParent += (_, _) => RefreshMenuMetrics(menu);
        menu.HandleCreated += (_, _) => RefreshMenuMetrics(menu);
    }

    internal static void RefreshMenuMetrics(ToolStripDropDownMenu menu)
    {
        int dpi = menu.DeviceDpi;
        menu.Padding = new Padding(Scale(3, dpi));
        menu.ImageScalingSize = new Size(Scale(IconSize, dpi), Scale(IconSize, dpi));
        foreach (ToolStripItem item in menu.Items)
        {
            if (item is ToolStripMenuItem menuItem)
                menuItem.Padding = new Padding(Scale(10, dpi), Scale(5, dpi), Scale(12, dpi), Scale(5, dpi));
            else if (item is ToolStripSeparator separator)
                StyleSeparator(separator);
        }
    }

    internal static void StyleMenuItem(ToolStripMenuItem item, bool emphasized = false)
    {
        // ToolStrip measures the text itself. Fixed-width items can collapse to the
        // image column on high-DPI Windows configurations.
        item.AutoSize = true;
        item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
        item.TextImageRelation = TextImageRelation.ImageBeforeText;
        item.TextAlign = ContentAlignment.MiddleLeft;
        item.ImageAlign = ContentAlignment.MiddleCenter;
        item.ImageScaling = ToolStripItemImageScaling.None;
        // Keep one consistent vertical rhythm for regular and emphasized rows;
        // the renderer then centers the visible glyph pixels in that row.
        item.Padding = new Padding(10, 5, 12, 5);
        item.Margin = new Padding(0, 0, 0, 0);
        item.Font = emphasized ? EmphasisFont : MenuFont;
        item.DropDownDirection = ToolStripDropDownDirection.Right;
        item.ShowShortcutKeys = false;
    }

    internal static void StyleSeparator(ToolStripSeparator separator)
    {
        separator.AutoSize = false;
        separator.Height = Scale(8, separator.GetCurrentParent()?.DeviceDpi ?? 96);
        separator.Margin = Padding.Empty;
    }

    internal static void StyleForm(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        form.AutoScaleDimensions = new SizeF(96, 96);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.Font = MenuFont;
        form.Icon = TrayIconFactory.Create();
        ApplyRoundedCorners(form, CornerRadius);
    }

    internal static void StyleButton(RoundedButton button, bool primary = false, bool danger = false)
    {
        button.Font = MenuFont;
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = primary ? Accent : Surface;
        button.ForeColor = primary ? AccentText : Text;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.MouseOverBackColor = danger ? DangerHover : primary ? Accent : Hover;
        button.FlatAppearance.MouseDownBackColor = danger ? DangerHover : primary ? Accent : Pressed;
        button.Cursor = Cursors.Hand;
        button.AutoSize = true;
        button.MinimumSize = new Size(80, ButtonHeight);
        button.Padding = new Padding(10, 4, 10, 4);
        button.Margin = new Padding(4);
    }

    internal static void StyleCard(Control card, int radius = CornerRadius)
    {
        card.BackColor = Surface;
        ApplyRoundedRegion(card, radius);
        card.Paint += (_, e) =>
        {
            if (card.Width <= 1 || card.Height <= 1) return;
            using var pen = new Pen(Border);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawRoundedRectangle(pen, 0.5F, 0.5F, card.Width - 1, card.Height - 1, Scale(radius, card.DeviceDpi));
        };
    }

    internal static void ApplyRoundedCorners(Control control, int radius = CornerRadius)
    {
        EventHandler apply = (_, _) => ApplyRoundedWindow(control, radius);
        control.HandleCreated += apply;
        control.SizeChanged += apply;
        control.DpiChangedAfterParent += apply;
        if (control.IsHandleCreated) ApplyRoundedWindow(control, radius);
    }

    internal static void ApplyRoundedRegion(Control control, int radius = CornerRadius)
    {
        EventHandler apply = (_, _) => SetManagedRoundedRegion(control, radius);
        control.SizeChanged += apply;
        control.DpiChangedAfterParent += apply;
        if (control.IsHandleCreated) SetManagedRoundedRegion(control, radius);
    }

    private static void ApplyRoundedWindow(Control control, int radius)
    {
        if (!control.IsHandleCreated || control.Width <= 0 || control.Height <= 0) return;
        int preference = NativeMethods.DwmWindowCornerRound;
        int result = NativeMethods.DwmSetWindowAttribute(control.Handle,
            NativeMethods.DwmwaWindowCornerPreference, ref preference, sizeof(int));
        if (result == 0)
        {
            // Remove a previous compatibility region once DWM is available so
            // the native shadow and resize frame remain intact.
            if (control is Form or ToolStripDropDown)
                NativeMethods.SetWindowRgn(control.Handle, IntPtr.Zero, true);
            return;
        }

        if (control is Form or ToolStripDropDown)
            SetNativeRoundedRegion(control, radius);
    }

    private static void SetNativeRoundedRegion(Control control, int radius)
    {
        if (!control.IsHandleCreated || control.Width <= 0 || control.Height <= 0) return;
        int size = Scale(radius, control.DeviceDpi) * 2;
        IntPtr region = NativeMethods.CreateRoundRectRgn(0, 0, control.Width + 1, control.Height + 1, size, size);
        if (region == IntPtr.Zero || NativeMethods.SetWindowRgn(control.Handle, region, true) == 0)
        {
            if (region != IntPtr.Zero) NativeMethods.DeleteObject(region);
        }
    }

    private static void SetManagedRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        int size = Scale(radius, control.DeviceDpi);
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        float diameter = size * 2F;
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(control.Width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(control.Width - diameter, control.Height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, control.Height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        Region? old = control.Region;
        control.Region = new Region(path);
        old?.Dispose();
    }

    internal static Label Label(string text, bool heading = false, bool muted = false) => new()
    {
        Text = text, AutoSize = true, ForeColor = muted ? MutedText : Text,
        Font = heading ? EmphasisFont : MenuFont, Dock = DockStyle.Fill,
        Margin = new Padding(0, 0, 0, Spacing), UseMnemonic = false
    };

    internal static Rectangle PlaceSubmenu(Rectangle owner, Rectangle row, Size popup, Rectangle workArea)
    {
        int width = Math.Min(popup.Width, workArea.Width);
        int height = Math.Min(popup.Height, workArea.Height);
        int x = owner.Right + width <= workArea.Right ? owner.Right : owner.Left - width;
        return new Rectangle(Math.Clamp(x, workArea.Left, workArea.Right - width),
            Math.Clamp(row.Top + (row.Height - height) / 2, workArea.Top, workArea.Bottom - height), width, height);
    }

    internal static void DrawOverlayHint(Graphics graphics, string text, Point location, Rectangle safeArea, int dpi)
    {
        int pad = Scale(12, dpi);
        Size size = TextRenderer.MeasureText(graphics, text, MenuFont,
            new Size(Math.Max(1, safeArea.Width - pad * 2), int.MaxValue), TextFormatFlags.NoPrefix);
        Size box = new(Math.Min(safeArea.Width, size.Width + pad * 2), Math.Min(safeArea.Height, size.Height + pad * 2));
        int x = Math.Clamp(location.X, safeArea.Left, Math.Max(safeArea.Left, safeArea.Right - box.Width));
        int y = Math.Clamp(location.Y, safeArea.Top, Math.Max(safeArea.Top, safeArea.Bottom - box.Height));
        using var brush = new SolidBrush(Color.FromArgb(242, 28, 35, 40));
        graphics.FillRoundedRectangle(brush, x, y, box.Width, box.Height, Scale(6, dpi));
        TextRenderer.DrawText(graphics, text, MenuFont, new Rectangle(x + pad, y + pad, box.Width - pad * 2, box.Height - pad * 2),
            Color.White, TextFormatFlags.NoPrefix);
    }

    private sealed class MenuColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Accent;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Pressed;
        public override Color MenuItemPressedGradientEnd => Pressed;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color ToolStripDropDownBackground => Surface;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Surface;
    }

    private sealed class CenteredMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Dictionary<(string Text, Font Font, float DpiX, float DpiY, int Width),
            (int Top, int Height, int LayoutHeight)> _textMetrics = new();

        internal CenteredMenuRenderer(ProfessionalColorTable colors) : base(colors)
        {
            RoundedEdges = true;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is ToolStripMenuItem)
            {
                Font font = e.TextFont ?? MenuFont;
                Rectangle bounds = e.TextRectangle;
                var ink = MeasureTextInk(e.Graphics, e.Text ?? string.Empty, font, bounds.Width);
                // ToolStrip translates Graphics to the current item's origin.
                // Center the visible ink in that local row, not in the native
                // text rectangle (which includes font leading and menu padding).
                int rowHeight = e.Item.Height > 0 ? e.Item.Height : bounds.Height;
                Rectangle textBounds = new(bounds.Left,
                    (int)Math.Round((rowHeight - ink.Height) / 2.0) - ink.Top,
                    bounds.Width, ink.LayoutHeight);
                // GDI's visible glyphs retain a small amount of lower-side
                // antialiasing even after the ink bounds are centered. Apply
                // one device pixel so submenu and top-level rows share the
                // same optical baseline.
                textBounds.Y += 1;
                Color textColor = !e.Item.Enabled ? DisabledText
                    : SystemInformation.HighContrast && e.Item.Selected ? SystemColors.HighlightText
                    : e.TextFormat.HasFlag(TextFormatFlags.Right) ? MutedText : e.TextColor;
                TextRenderer.DrawText(e.Graphics, e.Text, font, textBounds, textColor,
                    (e.TextFormat & (TextFormatFlags.Right |
                        TextFormatFlags.RightToLeft)) |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                return;
            }

            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Enabled || (!e.Item.Selected && !e.Item.Pressed)) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(e.Item.Pressed ? Pressed : Hover);
            e.Graphics.FillRoundedRectangle(brush, 2, 1, e.Item.Width - 4, e.Item.Height - 2, Scale(4, e.ToolStrip?.DeviceDpi ?? 96));
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            int inset = Scale(10, e.ToolStrip?.DeviceDpi ?? 96);
            e.Graphics.DrawLine(pen, inset, e.Item.Height / 2, e.Item.Width - inset, e.Item.Height / 2);
        }

        private (int Top, int Height, int LayoutHeight) MeasureTextInk(
            Graphics graphics, string text, Font font, int width)
        {
            var key = (text, font, graphics.DpiX, graphics.DpiY, width);
            if (_textMetrics.TryGetValue(key, out var cached)) return cached;
            const TextFormatFlags flags = TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            Size measured = TextRenderer.MeasureText(graphics, text, font,
                new Size(Math.Max(1, width), int.MaxValue), flags);
            using var probe = new Bitmap(Math.Max(1, Math.Min(width, measured.Width)),
                Math.Max(1, measured.Height));
            probe.SetResolution(graphics.DpiX, graphics.DpiY);
            using (Graphics probeGraphics = Graphics.FromImage(probe))
            {
                probeGraphics.Clear(Color.White);
                TextRenderer.DrawText(probeGraphics, text, font,
                    new Rectangle(Point.Empty, probe.Size), Color.Black, flags);
            }
            int top = probe.Height, bottom = -1;
            for (int y = 0; y < probe.Height; y++)
                for (int x = 0; x < probe.Width; x++)
                {
                    Color pixel = probe.GetPixel(x, y);
                    if (Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) >= 160) continue;
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            var metrics = bottom < top ? (0, measured.Height, measured.Height)
                : (top, bottom - top + 1, measured.Height);
            // Window titles and counts change; only cache a bounded set of metrics.
            if (_textMetrics.Count >= 128) _textMetrics.Clear();
            _textMetrics[key] = metrics;
            return metrics;
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item is ToolStripMenuItem && e.Image is not null)
            {
                Rectangle slot = e.ImageRectangle;
                Size imageSize = e.Image.Size;
                if (imageSize.Width != slot.Width || imageSize.Height != slot.Height)
                {
                    double scale = Math.Min(slot.Width / (double)imageSize.Width,
                        slot.Height / (double)imageSize.Height);
                    imageSize = new Size(Math.Max(1, (int)Math.Round(imageSize.Width * scale)),
                        Math.Max(1, (int)Math.Round(imageSize.Height * scale)));
                }

                Rectangle centered = new(
                    slot.Left + (slot.Width - imageSize.Width) / 2,
                    slot.Top + (slot.Height - imageSize.Height) / 2,
                    imageSize.Width,
                    imageSize.Height);
                if (!e.Item.Enabled)
                {
                    if (centered.Size == e.Image.Size)
                        ControlPaint.DrawImageDisabled(e.Graphics, e.Image, centered.X, centered.Y, Surface);
                    else
                    {
                        using var fitted = new Bitmap(centered.Width, centered.Height);
                        using (Graphics graphics = Graphics.FromImage(fitted))
                        {
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                            graphics.DrawImage(e.Image, new Rectangle(Point.Empty, fitted.Size), 0, 0,
                                e.Image.Width, e.Image.Height, GraphicsUnit.Pixel);
                        }
                        ControlPaint.DrawImageDisabled(e.Graphics, fitted, centered.X, centered.Y, Surface);
                    }
                }
                else if (centered.Size == e.Image.Size)
                    e.Graphics.DrawImageUnscaled(e.Image, centered.Location);
                else
                {
                    var state = e.Graphics.Save();
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    e.Graphics.DrawImage(e.Image, centered, 0, 0, e.Image.Width, e.Image.Height, GraphicsUnit.Pixel);
                    e.Graphics.Restore(state);
                }
                return;
            }

            base.OnRenderItemImage(e);
        }

    }
}
