using System.Drawing.Drawing2D;

namespace TopMostTool;

internal static class UiTheme
{
    // Pure Apple / CleanShot X minimalist light palette: calm neutral surfaces,
    // whisper-thin hairlines, disciplined monochrome ink, zero decorative color noise.
    internal static Color Background => SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(245, 245, 247);
    internal static Color Surface => SystemInformation.HighContrast ? SystemColors.Window : Color.White;
    internal static Color SurfaceMuted => SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(238, 238, 241);
    internal static Color SurfaceSubtle => SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(250, 250, 252);
    internal static Color SurfaceElevated => SystemInformation.HighContrast ? SystemColors.Window : Color.White;
    internal static Color Input => SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(245, 245, 247);
    internal static Color Text => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(29, 29, 31);
    internal static Color MutedText => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(110, 110, 115);
    internal static Color DisabledText => SystemInformation.HighContrast ? SystemColors.GrayText : Color.FromArgb(174, 174, 178);
    internal static Color Border => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(214, 214, 219);
    internal static Color BorderSubtle => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(234, 234, 238);
    internal static Color Accent => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(29, 29, 31);
    internal static Color AccentHover => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(52, 52, 56);
    internal static Color AccentPressed => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(12, 12, 14);
    internal static Color AccentSoft => SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(239, 239, 242);
    internal static Color AccentText => SystemInformation.HighContrast ? SystemColors.HighlightText : Color.White;
    internal static Color Hover => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(239, 239, 242);
    internal static Color Pressed => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(228, 228, 233);
    internal static Color Error => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(217, 48, 37);
    internal static Color ErrorSoft => SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(253, 242, 242);
    internal static Color ErrorBorder => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(245, 208, 206);
    internal static Color DangerHover => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(252, 232, 230);
    internal static Color Success => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(30, 142, 62);
    internal static Color SuccessSoft => SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(245, 245, 247);

    internal static readonly Font MenuFont = new(SystemFonts.MessageBoxFont!.FontFamily, Math.Max(9F, SystemFonts.MessageBoxFont.Size));
    internal static readonly Font CaptionFont = new(MenuFont.FontFamily, Math.Max(8.5F, MenuFont.Size - 0.5F));
    internal static readonly Font CaptionBoldFont = new(MenuFont.FontFamily, Math.Max(8.5F, MenuFont.Size - 0.5F), FontStyle.Bold);
    internal static readonly Font EmphasisFont = new(MenuFont, FontStyle.Bold);
    internal static readonly Font HeadingFont = new(MenuFont.FontFamily, MenuFont.Size + 6.5F, FontStyle.Bold);
    internal static readonly Font DisplayFont = new(MenuFont.FontFamily, MenuFont.Size + 9.5F, FontStyle.Bold);

    internal const int IconSize = 20;
    internal const int ButtonHeight = 34;
    internal const int Spacing = 8;
    internal const int PagePadding = 24;
    internal const int CornerRadius = 10;

    internal static string AppVersionLabel { get; } = FormatAppVersion();

    private static string FormatAppVersion()
    {
        Version? version = typeof(UiTheme).Assembly.GetName().Version;
        return version is null ? "v2.0.0" : $"v{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
    }

    internal static int Scale(int value, int dpi) => Math.Max(1, (int)Math.Round(value * dpi / 96.0));

    internal static void StyleMenu(ToolStripDropDownMenu menu)
    {
        menu.Renderer = new CenteredMenuRenderer(new MenuColorTable());
        menu.BackColor = Surface;
        menu.ForeColor = Text;
        menu.ShowItemToolTips = true;
        menu.AutoSize = true;
        menu.Font = MenuFont;
        menu.Padding = new Padding(5);
        ApplyRoundedCorners(menu, CornerRadius);
        menu.Opening += (_, _) => RefreshMenuMetrics(menu);
        menu.DpiChangedAfterParent += (_, _) => RefreshMenuMetrics(menu);
        menu.HandleCreated += (_, _) => RefreshMenuMetrics(menu);
    }

    internal static void RefreshMenuMetrics(ToolStripDropDownMenu menu)
    {
        int dpi = menu.DeviceDpi;
        menu.Padding = new Padding(Scale(4, dpi));
        menu.ImageScalingSize = new Size(Scale(IconSize, dpi), Scale(IconSize, dpi));
        foreach (ToolStripItem item in menu.Items)
        {
            if (item is ToolStripMenuItem menuItem)
                menuItem.Padding = new Padding(Scale(8, dpi), Scale(4, dpi), Scale(14, dpi), Scale(4, dpi));
            else if (item is ToolStripSeparator separator)
                StyleSeparator(separator);
        }
    }

    internal static void StyleMenuItem(ToolStripMenuItem item, bool emphasized = false)
    {
        item.AutoSize = true;
        item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
        item.TextImageRelation = TextImageRelation.ImageBeforeText;
        item.TextAlign = ContentAlignment.MiddleLeft;
        item.ImageAlign = ContentAlignment.MiddleCenter;
        item.ImageScaling = ToolStripItemImageScaling.None;
        item.Padding = new Padding(10, 6, 12, 6);
        item.Margin = Padding.Empty;
        item.Font = emphasized ? EmphasisFont : MenuFont;
        item.DropDownDirection = ToolStripDropDownDirection.Right;
        item.ShowShortcutKeys = false;
    }

    internal static void StyleSeparator(ToolStripSeparator separator)
    {
        separator.AutoSize = false;
        separator.Height = Scale(9, separator.GetCurrentParent()?.DeviceDpi ?? 96);
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
        EventHandler applyDialogChrome = (_, _) => ApplyDialogChrome(form);
        form.HandleCreated += applyDialogChrome;
        form.DpiChangedAfterParent += applyDialogChrome;
        if (form.IsHandleCreated) ApplyDialogChrome(form);
    }

    private static void ApplyDialogChrome(Form form)
    {
        if (!form.IsHandleCreated || SystemInformation.HighContrast ||
            form.FormBorderStyle is FormBorderStyle.None or FormBorderStyle.FixedToolWindow or FormBorderStyle.SizableToolWindow)
            return;
        int darkMode = 0;
        NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DwmwaUseImmersiveDarkModeBefore20H1, ref darkMode, sizeof(int));
        // Seamlessly blend the native Windows 11 title bar into the #F5F5F7 canvas.
        int caption = ColorRef(Background), text = ColorRef(Text), border = ColorRef(Border);
        NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DwmwaCaptionColor, ref caption, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DwmwaTextColor, ref text, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DwmwaBorderColor, ref border, sizeof(int));
        int corners = NativeMethods.DwmWindowCornerRound;
        NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DwmwaWindowCornerPreference, ref corners, sizeof(int));
    }

    private static int ColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    internal static void StyleButton(RoundedButton button, bool primary = false, bool danger = false)
    {
        button.Font = primary ? EmphasisFont : MenuFont;
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = primary ? Accent : Surface;
        button.ForeColor = primary ? AccentText : danger ? Error : Text;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.MouseOverBackColor = danger ? DangerHover : primary ? AccentHover : Hover;
        button.FlatAppearance.MouseDownBackColor = danger ? DangerHover : primary ? AccentPressed : Pressed;
        button.Cursor = Cursors.Hand;
        button.AutoSize = true;
        button.MinimumSize = new Size(80, ButtonHeight);
        button.Padding = new Padding(14, 5, 14, 5);
        button.Margin = new Padding(4);
    }

    internal static void StyleModifier(ModifierKeyCap checkBox)
    {
        checkBox.Appearance = Appearance.Button;
        checkBox.AutoSize = false;
        checkBox.TextAlign = ContentAlignment.MiddleCenter;
        checkBox.BackColor = Surface;
        checkBox.ForeColor = Text;
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.FlatAppearance.BorderSize = 0;
        checkBox.Cursor = Cursors.Hand;
        checkBox.Padding = new Padding(10, 4, 10, 4);
        checkBox.Margin = Padding.Empty;
        checkBox.MinimumSize = Size.Empty;
    }

    internal static void StyleCard(Control card, int radius = CornerRadius)
    {
        card.BackColor = Surface;
        ApplyRoundedRegion(card, radius);
        card.Paint += (_, e) =>
        {
            if (card.Width <= 1 || card.Height <= 1) return;
            using var pen = new Pen(BorderSubtle);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
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
        using var path = new GraphicsPath();
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
        int padX = Scale(14, dpi);
        int padY = Scale(10, dpi);
        const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
        Size size = TextRenderer.MeasureText(graphics, text, MenuFont,
            new Size(Math.Max(1, safeArea.Width - padX * 2), int.MaxValue), flags);
        Size box = new(
            Math.Min(safeArea.Width, size.Width + padX * 2),
            Math.Min(safeArea.Height, size.Height + padY * 2));
        int x = Math.Clamp(location.X, safeArea.Left, Math.Max(safeArea.Left, safeArea.Right - box.Width));
        int y = Math.Clamp(location.Y, safeArea.Top, Math.Max(safeArea.Top, safeArea.Bottom - box.Height));
        int radius = Scale(10, dpi);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // Soft diffused shadow
        using (var shadow = new SolidBrush(Color.FromArgb(46, 0, 0, 0)))
            graphics.FillRoundedRectangle(shadow, x, y + Scale(2, dpi), box.Width, box.Height, radius);

        // Clean Apple translucent dark HUD pill
        using (var brush = new SolidBrush(Color.FromArgb(234, 28, 28, 30)))
            graphics.FillRoundedRectangle(brush, x, y, box.Width, box.Height, radius);

        // Whisper-thin frost rim
        using (var border = new Pen(Color.FromArgb(48, 255, 255, 255)))
            graphics.DrawRoundedRectangle(border, x + 0.5F, y + 0.5F, box.Width - 1, box.Height - 1, radius);

        TextRenderer.DrawText(graphics, text, MenuFont,
            new Rectangle(x + padX, y + padY, box.Width - padX * 2, box.Height - padY * 2),
            Color.FromArgb(245, 245, 247), flags);
    }

    private sealed class MenuColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Pressed;
        public override Color MenuItemPressedGradientEnd => Pressed;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color ToolStripDropDownBackground => Surface;
        public override Color SeparatorDark => BorderSubtle;
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
                int rowHeight = e.Item.Height > 0 ? e.Item.Height : bounds.Height;
                int centeredTop = (rowHeight - ink.Height) / 2;
                Rectangle textBounds = new(bounds.Left,
                    centeredTop - ink.Top,
                    bounds.Width, ink.LayoutHeight);
                bool statusHeader = e.Item.Name == "StatusHeader";
                bool errorStatus = e.Item.Name == "ErrorStatus";
                Color textColor = statusHeader ? MutedText
                    : errorStatus ? Error
                    : !e.Item.Enabled ? DisabledText
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
            bool statusHeader = e.Item.Name == "StatusHeader";
            bool errorStatus = e.Item.Name == "ErrorStatus";
            int dpi = e.ToolStrip?.DeviceDpi ?? 96;
            int width = e.ToolStrip != null ? e.ToolStrip.ClientSize.Width : e.Item.Width;
            if (statusHeader)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int radius = Scale(6, dpi);
                using var brush = new SolidBrush(Background);
                e.Graphics.FillRoundedRectangle(brush, 3, 1, width - 6, e.Item.Height - 2, radius);
                return;
            }
            if (errorStatus)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int radius = Scale(6, dpi);
                using (var brush = new SolidBrush(ErrorSoft))
                    e.Graphics.FillRoundedRectangle(brush, 3, 1, width - 6, e.Item.Height - 2, radius);
                using (var border = new Pen(ErrorBorder))
                    e.Graphics.DrawRoundedRectangle(border, 3.5F, 1.5F, width - 7, e.Item.Height - 3, radius);
                return;
            }

            // Minimalist unchecked box for startup option when disabled
            if (e.Item is ToolStripMenuItem startupItem && startupItem.Text == "开机启动" && startupItem.Image == null)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int boxSize = Scale(15, dpi);
                int slotX = Scale(10, dpi);
                int slotY = (e.Item.Height - boxSize) / 2;
                int boxRadius = Scale(4, dpi);
                using var boxBg = new SolidBrush(Surface);
                e.Graphics.FillRoundedRectangle(boxBg, slotX, slotY, boxSize, boxSize, boxRadius);
                using var boxPen = new Pen(Color.FromArgb(199, 199, 204), 1.2F);
                e.Graphics.DrawRoundedRectangle(boxPen, slotX + 0.5F, slotY + 0.5F, boxSize - 1, boxSize - 1, boxRadius);
            }

            if (!e.Item.Enabled || (!e.Item.Selected && !e.Item.Pressed)) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool isDanger = e.Item.ForeColor == Error || e.Item.Name == "DangerAction";
            Color fill = isDanger ? DangerHover : e.Item.Pressed ? Pressed : Hover;
            using var fillBrush = new SolidBrush(fill);
            e.Graphics.FillRoundedRectangle(fillBrush, 3, 1, width - 6, e.Item.Height - 2, Scale(6, dpi));
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            if (e.Item is ToolStripMenuItem)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color arrowColor = !e.Item.Enabled ? DisabledText : e.Item.Selected ? Text : MutedText;
                using var pen = new Pen(arrowColor, 1.4F)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                Rectangle bounds = e.ArrowRectangle;
                int midX = bounds.Left + bounds.Width / 2;
                int midY = bounds.Top + bounds.Height / 2;
                int dpi = e.Item.GetCurrentParent()?.DeviceDpi ?? 96;
                int size = Scale(4, dpi);
                e.Graphics.DrawLines(pen, new[]
                {
                    new PointF(midX - size / 2F, midY - size),
                    new PointF(midX + size / 2F, midY),
                    new PointF(midX - size / 2F, midY + size)
                });
                return;
            }
            base.OnRenderArrow(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is not ToolStripDropDownMenu) { base.OnRenderToolStripBorder(e); return; }
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var border = new Pen(Border);
            e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F,
                e.ToolStrip.Width - 1, e.ToolStrip.Height - 1,
                Scale(8, e.ToolStrip.DeviceDpi));
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(BorderSubtle);
            int inset = Scale(10, e.ToolStrip?.DeviceDpi ?? 96);
            int width = e.ToolStrip != null ? e.ToolStrip.ClientSize.Width : e.Item.Width;
            e.Graphics.DrawLine(pen, inset, e.Item.Height / 2, width - inset, e.Item.Height / 2);
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
            if (_textMetrics.Count >= 128) _textMetrics.Clear();
            _textMetrics[key] = metrics;
            return metrics;
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item is ToolStripMenuItem startupItem && startupItem.Text == "开机启动" && e.Image is not null)
            {
                int dpi = e.ToolStrip?.DeviceDpi ?? 96;
                int boxSize = Scale(15, dpi);
                int slotX = e.ImageRectangle.Left + (e.ImageRectangle.Width - boxSize) / 2;
                int slotY = e.ImageRectangle.Top + (e.ImageRectangle.Height - boxSize) / 2;
                int boxRadius = Scale(4, dpi);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var checkBg = new SolidBrush(Accent))
                    e.Graphics.FillRoundedRectangle(checkBg, slotX, slotY, boxSize, boxSize, boxRadius);
                using var checkPen = new Pen(Color.White, 1.7F)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                e.Graphics.DrawLines(checkPen, new[]
                {
                    new PointF(slotX + boxSize * 0.24F, slotY + boxSize * 0.52F),
                    new PointF(slotX + boxSize * 0.44F, slotY + boxSize * 0.72F),
                    new PointF(slotX + boxSize * 0.76F, slotY + boxSize * 0.30F)
                });
                return;
            }

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
                if (!e.Item.Enabled && e.Item.Name != "StatusHeader")
                {
                    if (centered.Size == e.Image.Size)
                        ControlPaint.DrawImageDisabled(e.Graphics, e.Image, centered.X, centered.Y, Surface);
                    else
                    {
                        using var fitted = new Bitmap(centered.Width, centered.Height);
                        using (Graphics graphics = Graphics.FromImage(fitted))
                        {
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.Half;
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
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    e.Graphics.DrawImage(e.Image, centered, 0, 0, e.Image.Width, e.Image.Height, GraphicsUnit.Pixel);
                    e.Graphics.Restore(state);
                }
                return;
            }

            base.OnRenderItemImage(e);
        }
    }
}
