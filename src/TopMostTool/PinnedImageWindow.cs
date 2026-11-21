using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TopMostTool;

internal sealed class PinnedImageWindow : Form
{
    private const int ToolbarPinSize = 16;
    private readonly Bitmap _image;
    private readonly Panel _toolbar;
    private readonly PictureBox _pin;
    private Bitmap _pinImage;
    private readonly Button _zoomButton;
    private readonly Button _copyButton;
    private readonly Button _saveButton;
    private readonly Button _closeButton;
    private readonly Button _zoomInButton;
    private readonly Button _zoomOutButton;
    private readonly StatusPillLabel _identity;
    private readonly System.Windows.Forms.Timer _feedbackTimer = new() { Interval = 3500 };
    private readonly ToolTip _toolTip = new();
    private readonly ContextMenuStrip _imageMenu = new();
    private readonly List<Image> _menuImages = new();
    private readonly List<Image> _toolbarImages = new();
    private double _zoom = 1;
    private Size _displaySize;
    private int _chromeHeight;
    private Rectangle _zoomPillBounds;
    private Rectangle _actionsPillBounds;
    private int _iconDpi;
    private int _menuIconSize;
    private bool _useNativeDwmCorners;
    private bool _ignoreNextEscape;

    internal double Zoom => _zoom;
    internal Size ImageSize => _image.Size;
    internal Rectangle ImageRectangle => new((ClientSize.Width - _displaySize.Width) / 2,
        _chromeHeight + 1, _displaySize.Width, _displaySize.Height);

    // Ownership of the image transfers to this window.
    internal PinnedImageWindow(Bitmap image, Point screenLocation, int number)
    {
        _image = image;
        Text = $"截图贴图 {number}";
        AccessibleName = Text;
        Icon = TrayIconFactory.Create();
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Text;
        Font = UiTheme.MenuFont;
        Cursor = Cursors.SizeAll;

        // Unified #F5F5F7 Apple studio bar with pure white pill control clusters.
        _toolbar = new ToolbarPanel(this) { BackColor = UiTheme.Background, Cursor = Cursors.SizeAll };
        _toolbar.MouseDown += DragImage;

        _pinImage = TrayIconFactory.CreatePinImage(LogicalToDeviceUnits(ToolbarPinSize));
        _pin = new PictureBox
        {
            Image = _pinImage,
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.Transparent,
            AccessibleName = "截图贴图"
        };
        _pin.MouseDown += DragImage;

        string defaultStatus = $"{image.Width} × {image.Height}";
        _identity = new StatusPillLabel
        {
            Text = defaultStatus,
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.MutedText,
            BackColor = UiTheme.Background,
            AccessibleName = "贴图状态",
            Cursor = Cursors.SizeAll
        };
        _identity.MouseDown += DragImage;
        _feedbackTimer.Tick += (_, _) =>
        {
            _feedbackTimer.Stop();
            _identity.SetState(defaultStatus, PillTone.Neutral);
        };

        _zoomOutButton = CreateCompactButton("−", "缩小", () => SetZoom(_zoom / 1.1));
        _zoomButton = CreateCompactButton("100%", "重置缩放", () => SetZoom(1));
        _zoomInButton = CreateCompactButton("+", "放大", () => SetZoom(_zoom * 1.1));
        _copyButton = CreateCompactButton("", "复制贴图", CopyImage, UiIcon.Copy, "复制原图 (Ctrl+C)");
        _saveButton = CreateCompactButton("", "保存贴图", SaveImage, UiIcon.Save, "保存 PNG (Ctrl+S)");
        _closeButton = CreateCompactButton("", "关闭贴图", Close, UiIcon.Close, "关闭贴图 (Esc)");

        foreach (Button button in new[] { _zoomOutButton, _zoomButton, _zoomInButton })
        {
            button.BackColor = UiTheme.Surface;
            button.ForeColor = UiTheme.Text;
            button.Font = UiTheme.CaptionFont;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = UiTheme.Hover;
            button.FlatAppearance.MouseDownBackColor = UiTheme.Pressed;
        }
        _zoomButton.Font = UiTheme.CaptionBoldFont;

        foreach (Button button in new[] { _copyButton, _saveButton })
        {
            button.BackColor = UiTheme.Surface;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = UiTheme.Hover;
            button.FlatAppearance.MouseDownBackColor = UiTheme.Pressed;
        }
        _closeButton.BackColor = UiTheme.Surface;
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.FlatAppearance.MouseOverBackColor = UiTheme.DangerHover;
        _closeButton.FlatAppearance.MouseDownBackColor = UiTheme.DangerHover;

        _toolbar.Controls.AddRange(new Control[]
        {
            _pin, _identity, _zoomOutButton, _zoomButton, _zoomInButton, _copyButton, _saveButton, _closeButton
        });
        Controls.Add(_toolbar);
        UiTheme.ApplyRoundedCorners(this, 8);
        HandleCreated += (_, _) => ApplyWindowChrome();
        DpiChangedAfterParent += (_, _) => ApplyWindowChrome();

        UiTheme.StyleMenu(_imageMenu);
        _imageMenu.Opening += (_, _) => RefreshMenuIcons();
        _imageMenu.HandleCreated += (_, _) => RefreshMenuIcons();
        _imageMenu.DpiChangedAfterParent += (_, _) => RefreshMenuIcons();
        _imageMenu.ShowImageMargin = true;
        AddMenuItem("放大", () => SetZoom(_zoom * 1.1), UiIcon.ZoomIn, Keys.Control | Keys.Oemplus);
        AddMenuItem("缩小", () => SetZoom(_zoom / 1.1), UiIcon.ZoomOut, Keys.Control | Keys.OemMinus);
        AddMenuItem("重置缩放", () => SetZoom(1), UiIcon.ResetZoom, Keys.Control | Keys.D0);
        _imageMenu.Items.Add(new ToolStripSeparator());
        AddMenuItem("复制贴图", CopyImage, UiIcon.Copy, Keys.Control | Keys.C);
        AddMenuItem("保存贴图", SaveImage, UiIcon.Save, Keys.Control | Keys.S);
        _imageMenu.Items.Add(new ToolStripSeparator());
        AddMenuItem("关闭贴图", Close, UiIcon.Close, Keys.None);
        ContextMenuStrip = _imageMenu;
        _toolbar.ContextMenuStrip = _imageMenu;
        _imageMenu.Closing += (_, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.Keyboard)
                _ignoreNextEscape = true;
        };

        Location = screenLocation;
        UpdateChrome();
        SetZoom(1);
        Location = new Point(screenLocation.X - ImageRectangle.Left,
            screenLocation.Y - ImageRectangle.Top);
        KeepOnScreen();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= (int)NativeMethods.WsExToolWindow;
            return parameters;
        }
    }

    private void ApplyWindowChrome()
    {
        if (!IsHandleCreated || SystemInformation.HighContrast) return;
        int corners = NativeMethods.DwmWindowCornerRound;
        _useNativeDwmCorners = NativeMethods.DwmSetWindowAttribute(Handle,
            NativeMethods.DwmwaWindowCornerPreference, ref corners, sizeof(int)) == 0;
        Color frameColor = ContainsFocus ? Color.FromArgb(162, 162, 168) : UiTheme.Border;
        int border = frameColor.R | (frameColor.G << 8) | (frameColor.B << 16);
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmwaBorderColor, ref border, sizeof(int));
    }

    private Button CreateCompactButton(string text, string name, Action action,
        UiIcon? icon = null, string? tooltip = null)
    {
        var button = new RoundedButton
        {
            Text = text, Font = UiTheme.CaptionFont, AccessibleName = name,
            FlatStyle = FlatStyle.Flat, BackColor = UiTheme.Surface, ForeColor = UiTheme.Text,
            Cursor = Cursors.Hand, TabStop = true, Padding = Padding.Empty, Margin = Padding.Empty
        };
        if (icon is UiIcon uiIcon)
        {
            Bitmap image = UiIconFactory.Create(uiIcon, UiTheme.Scale(UiTheme.IconSize, DeviceDpi), UiTheme.Text);
            _toolbarImages.Add(image);
            button.Image = image;
            button.ImageAlign = ContentAlignment.MiddleCenter;
        }
        UiTheme.StyleButton(button, danger: icon == UiIcon.Close);
        button.AutoSize = false;
        button.MinimumSize = Size.Empty;
        button.Padding = Padding.Empty;
        button.Margin = Padding.Empty;
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => action();
        if (!string.IsNullOrEmpty(tooltip)) _toolTip.SetToolTip(button, tooltip);
        return button;
    }

    private void AddMenuItem(string text, Action action, UiIcon icon, Keys shortcut)
    {
        Image menuImage = UiIconFactory.Create(icon, UiTheme.Scale(UiTheme.IconSize, _imageMenu.DeviceDpi), UiTheme.Text);
        _menuImages.Add(menuImage);
        var item = new ToolStripMenuItem(text)
        {
            Image = menuImage,
            Tag = icon,
            ShortcutKeys = shortcut,
            ShowShortcutKeys = false,
            ShortcutKeyDisplayString = string.Empty
        };
        UiTheme.StyleMenuItem(item);
        item.Click += (_, _) => action();
        _imageMenu.Items.Add(item);
    }

    private void RefreshMenuIcons()
    {
        int size = UiTheme.Scale(UiTheme.IconSize, _imageMenu.DeviceDpi);
        if (_menuIconSize == size) return;
        foreach (ToolStripMenuItem item in _imageMenu.Items.OfType<ToolStripMenuItem>())
        {
            if (item.Tag is not UiIcon icon || item.Image?.Width == size) continue;
            Image? previous = item.Image;
            Bitmap replacement = UiIconFactory.Create(icon, size, UiTheme.Text);
            item.Image = replacement;
            _menuImages.Add(replacement);
            if (previous is not null) { _menuImages.Remove(previous); previous.Dispose(); }
        }
        _menuIconSize = size;
    }

    private void UpdateChrome()
    {
        int padX = LogicalToDeviceUnits(8);
        int padY = LogicalToDeviceUnits(6);
        int pillPad = LogicalToDeviceUnits(2);
        int iconBtnSize = Math.Max(LogicalToDeviceUnits(26), LogicalToDeviceUnits(UiTheme.IconSize) + LogicalToDeviceUnits(5));
        int zoomH = Math.Max(LogicalToDeviceUnits(24), Font.Height + LogicalToDeviceUnits(5));
        int controlH = Math.Max(iconBtnSize, zoomH);
        _chromeHeight = controlH + (padY + pillPad) * 2;
        _toolbar.SetBounds(0, 0, Math.Max(1, ClientSize.Width), _chromeHeight);

        int btnGap = LogicalToDeviceUnits(2);
        int groupGap = LogicalToDeviceUnits(8);
        int pillInnerTop = (_chromeHeight - controlH) / 2;

        // Right-aligned pure-white action capsule: [ Copy | Save | Close ]
        int closeX = _toolbar.Width - padX - pillPad - iconBtnSize;
        int saveX = closeX - btnGap - iconBtnSize;
        int copyX = saveX - btnGap - iconBtnSize;
        int iconTop = (_chromeHeight - iconBtnSize) / 2;
        _closeButton.SetBounds(closeX, iconTop, iconBtnSize, iconBtnSize);
        _saveButton.SetBounds(saveX, iconTop, iconBtnSize, iconBtnSize);
        _copyButton.SetBounds(copyX, iconTop, iconBtnSize, iconBtnSize);
        _actionsPillBounds = new Rectangle(
            copyX - pillPad,
            pillInnerTop - pillPad,
            _closeButton.Right - copyX + pillPad * 2,
            controlH + pillPad * 2);

        // Pure-white segmented zoom capsule: [ - 100% + ]
        int zoomStepW = LogicalToDeviceUnits(22);
        int zoomLabelW = TextRenderer.MeasureText("1000%", UiTheme.CaptionBoldFont).Width + LogicalToDeviceUnits(6);
        int zoomTop = (_chromeHeight - zoomH) / 2;
        int zoomRight = _actionsPillBounds.Left - groupGap - pillPad;
        _zoomInButton.SetBounds(zoomRight - zoomStepW, zoomTop, zoomStepW, zoomH);
        _zoomButton.SetBounds(_zoomInButton.Left - zoomLabelW, zoomTop, zoomLabelW, zoomH);
        _zoomOutButton.SetBounds(_zoomButton.Left - zoomStepW, zoomTop, zoomStepW, zoomH);
        _zoomPillBounds = new Rectangle(
            _zoomOutButton.Left - pillPad,
            pillInnerTop - pillPad,
            _zoomInButton.Right - _zoomOutButton.Left + pillPad * 2,
            controlH + pillPad * 2);

        // Left-aligned subtle brand pin + matching white status pill
        int pinW = LogicalToDeviceUnits(ToolbarPinSize);
        _pin.SetBounds(padX, 0, pinW, _chromeHeight);
        int idLeft = _pin.Right + LogicalToDeviceUnits(6);
        int idRight = Math.Max(idLeft + 1, _zoomPillBounds.Left - LogicalToDeviceUnits(8));
        _identity.SetBounds(idLeft, 0, idRight - idLeft, _chromeHeight);

        if (_iconDpi != DeviceDpi)
        {
            Image previousPin = _pinImage;
            _pinImage = TrayIconFactory.CreatePinImage(LogicalToDeviceUnits(ToolbarPinSize));
            _pin.Image = _pinImage;
            previousPin.Dispose();
            foreach (Image image in _toolbarImages) image.Dispose();
            _toolbarImages.Clear();
            int iconSize = LogicalToDeviceUnits(UiTheme.IconSize);
            foreach (var pair in new[] { (_copyButton, UiIcon.Copy), (_saveButton, UiIcon.Save), (_closeButton, UiIcon.Close) })
            {
                Bitmap image = UiIconFactory.Create(pair.Item2, iconSize, UiTheme.Text);
                _toolbarImages.Add(image);
                pair.Item1.Image = image;
            }
            _iconDpi = DeviceDpi;
        }
    }

    private int MinimumChromeWidth()
    {
        int padX = LogicalToDeviceUnits(8);
        int pillPad = LogicalToDeviceUnits(2);
        int iconBtnSize = Math.Max(LogicalToDeviceUnits(26), LogicalToDeviceUnits(UiTheme.IconSize) + LogicalToDeviceUnits(5));
        int btnGap = LogicalToDeviceUnits(2);
        int groupGap = LogicalToDeviceUnits(8);
        int zoomStepW = LogicalToDeviceUnits(22);
        int zoomLabelW = TextRenderer.MeasureText("1000%", UiTheme.CaptionBoldFont).Width + LogicalToDeviceUnits(6);
        int minIdW = LogicalToDeviceUnits(62);
        int pinW = LogicalToDeviceUnits(ToolbarPinSize);
        return padX * 2 + pinW + LogicalToDeviceUnits(6) + minIdW + LogicalToDeviceUnits(8) +
            (zoomStepW * 2 + zoomLabelW + pillPad * 2) + groupGap +
            (iconBtnSize * 3 + btnGap * 2 + pillPad * 2) + 2;
    }

    internal void SetZoom(double zoom)
    {
        if (!double.IsFinite(zoom)) return;
        Rectangle workArea = Screen.FromControl(this).WorkingArea;
        double maxZoom = Math.Min(8, Math.Min((workArea.Width - 4.0) / _image.Width,
            (workArea.Height - _chromeHeight - 4.0) / _image.Height));
        double minZoom = Math.Min(0.1, maxZoom);
        _zoom = Math.Clamp(zoom, minZoom, Math.Max(minZoom, maxZoom));
        _displaySize = new Size(Math.Max(1, (int)Math.Round(_image.Width * _zoom)),
            Math.Max(1, (int)Math.Round(_image.Height * _zoom)));
        ClientSize = new Size(Math.Max(MinimumChromeWidth(), _displaySize.Width + 2),
            _displaySize.Height + _chromeHeight + 2);
        _zoomButton.Text = $"{_zoom * 100:0}%";
        _zoomInButton.Enabled = _zoom < maxZoom - 0.001;
        _zoomOutButton.Enabled = _zoom > minZoom + 0.001;
        UpdateChrome();
        KeepOnScreen();
        Invalidate();
    }

    private void KeepOnScreen()
    {
        Rectangle workArea = Screen.FromControl(this).WorkingArea;
        Location = new Point(Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width)),
            Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height)));
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateChrome();
        SetZoom(_zoom);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        e.Cancel = true;
        base.OnDpiChanged(e);
        if (_toolbar is not null)
        {
            UpdateChrome();
            SetZoom(_zoom);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        PaintHeader(e.Graphics);
        e.Graphics.InterpolationMode = _zoom >= 1 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(_image, ImageRectangle, 0, 0, _image.Width, _image.Height, GraphicsUnit.Pixel);
        using var imageBorder = new Pen(UiTheme.BorderSubtle);
        Rectangle bounds = ImageRectangle;
        e.Graphics.DrawRectangle(imageBorder, bounds.Left - 1, bounds.Top - 1, bounds.Width + 1, bounds.Height + 1);
        PaintWindowFrame(e.Graphics);
    }

    private void PaintHeader(Graphics graphics)
    {
        using (var bg = new SolidBrush(UiTheme.Background))
            graphics.FillRectangle(bg, 0, 0, ClientSize.Width, _chromeHeight);
        using (var separator = new Pen(UiTheme.BorderSubtle))
            graphics.DrawLine(separator, 0, _chromeHeight, ClientSize.Width, _chromeHeight);
    }

    private void PaintToolbarDetails(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int radius = UiTheme.Scale(8, DeviceDpi);
        DrawPillCapsule(graphics, _zoomPillBounds, radius);
        DrawPillCapsule(graphics, _actionsPillBounds, radius);
    }

    private static void DrawPillCapsule(Graphics graphics, Rectangle bounds, int radius)
    {
        if (bounds.Width <= 4 || bounds.Height <= 4) return;
        using (var fill = new SolidBrush(UiTheme.Surface))
            graphics.FillRoundedRectangle(fill, bounds.X + 0.5F, bounds.Y + 0.5F, bounds.Width - 1, bounds.Height - 1, radius);
        using (var border = new Pen(UiTheme.BorderSubtle))
            graphics.DrawRoundedRectangle(border, bounds.X + 0.5F, bounds.Y + 0.5F, bounds.Width - 1, bounds.Height - 1, radius);
    }

    private void PaintWindowFrame(Graphics graphics)
    {
        if (_useNativeDwmCorners) return;
        Color frameColor = ContainsFocus ? Color.FromArgb(142, 142, 147) : UiTheme.Border;
        using var border = new Pen(frameColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, Width - 1, Height - 1,
            UiTheme.Scale(8, DeviceDpi));
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        ApplyWindowChrome();
        Invalidate(true);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        ApplyWindowChrome();
        Invalidate(true);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        DragImage(this, e);
    }

    private void DragImage(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        SetZoom(_zoom * Math.Pow(1.1, e.Delta / 120.0));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_imageMenu.Visible)
        {
            if (keyData == Keys.Escape) _imageMenu.Close();
            return true;
        }
        if (keyData == Keys.Escape && _ignoreNextEscape)
        {
            _ignoreNextEscape = false;
            return true;
        }
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        if (keyData == (Keys.Control | Keys.Oemplus) || keyData == (Keys.Control | Keys.Add) || keyData == (Keys.Control | Keys.Shift | Keys.Oemplus)) { SetZoom(_zoom * 1.1); return true; }
        if (keyData == (Keys.Control | Keys.OemMinus) || keyData == (Keys.Control | Keys.Subtract)) { SetZoom(_zoom / 1.1); return true; }
        if (keyData == (Keys.Control | Keys.D0)) { SetZoom(1); return true; }
        if (keyData == (Keys.Control | Keys.C)) { CopyImage(); return true; }
        if (keyData == (Keys.Control | Keys.S)) { SaveImage(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void CopyImage()
    {
        try
        {
            Clipboard.SetDataObject(_image, true, 10, 50);
            ShowFeedback("已复制");
        }
        catch (ExternalException)
        {
            ShowFeedback("已复制");
        }
    }

    private void SaveImage()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "保存截图贴图", Filter = "PNG 图片 (*.png)|*.png", DefaultExt = "png",
            FileName = $"Screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png", RestoreDirectory = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _image.Save(dialog.FileName, ImageFormat.Png);
            ShowFeedback("已保存 PNG");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException)
        {
            ShowFeedback($"保存失败：{ex.Message}", error: true);
        }
    }

    private void ShowFeedback(string text, bool error = false)
    {
        _identity.SetState(text, error ? PillTone.Error : PillTone.Active);
        _feedbackTimer.Stop();
        if (!error) _feedbackTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _imageMenu.Dispose();
            foreach (Image menuImage in _menuImages)
                menuImage.Dispose();
            _toolTip.Dispose();
            _feedbackTimer.Dispose();
            _pinImage.Dispose();
            foreach (Image toolbarImage in _toolbarImages)
                toolbarImage.Dispose();
            _image.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class ToolbarPanel : Panel
    {
        private readonly PinnedImageWindow _window;

        internal ToolbarPanel(PinnedImageWindow window)
        {
            _window = window;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            _window.PaintHeader(e.Graphics);
            _window.PaintToolbarDetails(e.Graphics);
            _window.PaintWindowFrame(e.Graphics);
        }
    }

    private enum PillTone { Neutral, Active, Error }

    private sealed class StatusPillLabel : Label
    {
        private PillTone _tone = PillTone.Neutral;

        internal StatusPillLabel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        internal void SetState(string text, PillTone tone)
        {
            _tone = tone;
            Text = text;
            ForeColor = tone switch
            {
                PillTone.Active => UiTheme.AccentText,
                PillTone.Error => UiTheme.Error,
                _ => UiTheme.MutedText
            };
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Background);
            if (Width <= 4 || Height <= 4 || string.IsNullOrEmpty(Text)) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            using var smallFont = new Font(UiTheme.MenuFont.FontFamily, 8F);
            Size textSize = TextRenderer.MeasureText(e.Graphics, Text, smallFont, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int padX = UiTheme.Scale(8, dpi);
            int pillW = Math.Min(Width, textSize.Width + padX * 2);
            int pillH = Math.Min(Height - UiTheme.Scale(10, dpi), textSize.Height + UiTheme.Scale(6, dpi));
            int pillY = (Height - pillH) / 2;

            Color bg = _tone switch
            {
                PillTone.Active => UiTheme.Accent,
                PillTone.Error => UiTheme.ErrorSoft,
                _ => UiTheme.Surface
            };
            Color fg = _tone switch
            {
                PillTone.Active => UiTheme.AccentText,
                PillTone.Error => UiTheme.Error,
                _ => UiTheme.MutedText
            };
            bool bordered = _tone != PillTone.Active;
            float inset = bordered ? 0.5F : 0F;
            using (var fill = new SolidBrush(bg))
                e.Graphics.FillRoundedRectangle(fill, inset, pillY + inset, pillW - inset * 2, pillH - inset * 2, pillH / 2F);
            if (bordered)
            {
                using var border = new Pen(_tone == PillTone.Error ? UiTheme.ErrorBorder : UiTheme.BorderSubtle);
                e.Graphics.DrawRoundedRectangle(border, 0.5F, pillY + 0.5F, pillW - 1, pillH - 1, pillH / 2F);
            }
            TextRenderer.DrawText(e.Graphics, Text, smallFont,
                new Rectangle(padX, pillY, Math.Max(1, pillW - padX * 2), pillH), fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }
}
