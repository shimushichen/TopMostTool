using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TopMostTool;

internal sealed class PinnedImageWindow : Form
{
    private const int ToolbarPinSize = 20;
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
    private readonly Label _identity;
    private readonly System.Windows.Forms.Timer _feedbackTimer = new() { Interval = 3500 };
    private readonly ToolTip _toolTip = new();
    private readonly ContextMenuStrip _imageMenu = new();
    private readonly List<Image> _menuImages = new();
    private readonly List<Image> _toolbarImages = new();
    private double _zoom = 1;
    private Size _displaySize;
    private int _chromeHeight;
    private int _iconDpi;
    private int _menuIconSize;
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

        // The toolbar shares the window's outer frame, rather than adding a
        // second rounded card around the title and controls.
        _toolbar = new ToolbarPanel(this) { BackColor = UiTheme.Surface, Cursor = Cursors.SizeAll };
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
        _toolTip.SetToolTip(_pin, "贴图保持在最上层");
        _identity = new Label
        {
            Text = $"贴图 {number} · {image.Width} × {image.Height}", AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = UiTheme.MutedText,
            AccessibleName = "贴图状态", Cursor = Cursors.SizeAll
        };
        _identity.MouseDown += DragImage;
        _feedbackTimer.Tick += (_, _) =>
        {
            _feedbackTimer.Stop();
            _identity.Text = $"贴图 {number} · {image.Width} × {image.Height}";
            _identity.ForeColor = UiTheme.MutedText;
            _toolTip.SetToolTip(_identity, "拖动移动 · 滚轮缩放 · 右键打开菜单");
        };
        _toolTip.SetToolTip(_identity, "拖动移动 · 滚轮缩放 · 右键打开菜单");
        _zoomButton = CreateButton("100%", "重置缩放", () => SetZoom(1));
        _zoomButton.Font = UiTheme.MenuFont;
        _zoomOutButton = CreateButton("−", "缩小", () => SetZoom(_zoom / 1.1));
        _zoomInButton = CreateButton("+", "放大", () => SetZoom(_zoom * 1.1));
        _copyButton = CreateButton("复制", "复制贴图", CopyImage, MenuActionIcon.Copy);
        _saveButton = CreateButton("保存", "保存贴图", SaveImage, MenuActionIcon.Save);
        _closeButton = CreateButton("", "关闭贴图", Close, MenuActionIcon.Close);
        _copyButton.BackColor = UiTheme.Hover;
        _closeButton.FlatAppearance.MouseOverBackColor = UiTheme.DangerHover;
        _toolbar.Controls.AddRange(new Control[] { _pin, _identity, _zoomOutButton, _zoomButton, _zoomInButton, _copyButton, _saveButton, _closeButton });
        Controls.Add(_toolbar);
        UiTheme.ApplyRoundedCorners(this, UiTheme.CornerRadius);

        UiTheme.StyleMenu(_imageMenu);
        _imageMenu.Opening += (_, _) => RefreshMenuIcons();
        _imageMenu.HandleCreated += (_, _) => RefreshMenuIcons();
        _imageMenu.DpiChangedAfterParent += (_, _) => RefreshMenuIcons();
        _imageMenu.ShowImageMargin = true;
        AddMenuItem("放大", () => SetZoom(_zoom * 1.1), MenuActionIcon.ZoomIn, Keys.Control | Keys.Oemplus);
        AddMenuItem("缩小", () => SetZoom(_zoom / 1.1), MenuActionIcon.ZoomOut, Keys.Control | Keys.OemMinus);
        AddMenuItem("重置缩放", () => SetZoom(1), MenuActionIcon.ResetZoom, Keys.Control | Keys.D0);
        _imageMenu.Items.Add(new ToolStripSeparator());
        AddMenuItem("复制贴图", CopyImage, MenuActionIcon.Copy, Keys.Control | Keys.C);
        AddMenuItem("保存贴图", SaveImage, MenuActionIcon.Save, Keys.Control | Keys.S);
        _imageMenu.Items.Add(new ToolStripSeparator());
        AddMenuItem("关闭贴图", Close, MenuActionIcon.Close, Keys.None);
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

    private Button CreateButton(string text, string name, Action action, MenuActionIcon? icon = null)
    {
        var button = new RoundedButton
        {
            Text = text, Font = UiTheme.MenuFont, AccessibleName = name,
            FlatStyle = FlatStyle.Flat, BackColor = UiTheme.Surface, ForeColor = UiTheme.Text,
            Cursor = Cursors.Hand, TabStop = true, Padding = Padding.Empty, Margin = Padding.Empty
        };
        if (icon is MenuActionIcon actionIcon)
        {
            Bitmap image = MenuIconFactory.Create(actionIcon, 20);
            _toolbarImages.Add(image);
            button.Image = image;
            button.ImageAlign = ContentAlignment.MiddleCenter;
        }
        UiTheme.StyleButton(button, danger: icon == MenuActionIcon.Close);
        button.AutoSize = false;
        button.MinimumSize = Size.Empty;
        button.Padding = Padding.Empty;
        button.Margin = Padding.Empty;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.FlatAppearance.BorderSize = icon == MenuActionIcon.Close ? 0 : 1;
        button.Click += (_, _) => action();
        _toolTip.SetToolTip(button, name);
        return button;
    }

    private void AddMenuItem(string text, Action action, MenuActionIcon icon, Keys shortcut)
    {
        Image menuImage = MenuIconFactory.Create(icon, UiTheme.Scale(UiTheme.IconSize, _imageMenu.DeviceDpi));
        _menuImages.Add(menuImage);
        var item = new ToolStripMenuItem(text)
        {
            Image = menuImage,
            Tag = icon,
            ShortcutKeys = shortcut,
            // Keep the real bindings active for keyboard users while keeping
            // the screenshot context menu visually compact.
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
            if (item.Tag is not MenuActionIcon action || item.Image?.Width == size) continue;
            Image? previous = item.Image;
            Bitmap replacement = MenuIconFactory.Create(action, size);
            item.Image = replacement;
            _menuImages.Add(replacement);
            if (previous is not null) { _menuImages.Remove(previous); previous.Dispose(); }
        }
        _menuIconSize = size;
    }

    private void UpdateChrome()
    {
        int pad = LogicalToDeviceUnits(UiTheme.Spacing);
        int titleHeight = Math.Max(LogicalToDeviceUnits(28), Font.Height + pad);
        int rowHeight = Math.Max(LogicalToDeviceUnits(UiTheme.ButtonHeight), Font.Height + pad * 2);
        _chromeHeight = titleHeight + rowHeight + pad * 2;
        _toolbar.SetBounds(1, 1, ClientSize.Width - 2, _chromeHeight);
        _pin.SetBounds(pad, 0, LogicalToDeviceUnits(ToolbarPinSize), titleHeight);
        _closeButton.SetBounds(_toolbar.Width - titleHeight - pad, 0, titleHeight, titleHeight);
        int titleLeft = _pin.Right + LogicalToDeviceUnits(2);
        _identity.SetBounds(titleLeft, 0, Math.Max(1, _closeButton.Left - titleLeft - pad / 2), titleHeight);
        int square = rowHeight;
        _zoomOutButton.SetBounds(pad, titleHeight + pad, square, rowHeight);
        _zoomButton.SetBounds(_zoomOutButton.Right + pad / 2, titleHeight + pad, TextRenderer.MeasureText("1000%", Font).Width + pad, rowHeight);
        _zoomInButton.SetBounds(_zoomButton.Right + pad / 2, titleHeight + pad, square, rowHeight);
        int actionWidth = TextRenderer.MeasureText("保存", Font).Width + LogicalToDeviceUnits(UiTheme.IconSize) + pad * 2;
        _saveButton.SetBounds(_toolbar.Width - pad - actionWidth, titleHeight + pad, actionWidth, rowHeight);
        _copyButton.SetBounds(_saveButton.Left - pad - actionWidth, titleHeight + pad, actionWidth, rowHeight);
        if (_iconDpi != DeviceDpi)
        {
            Image previousPin = _pinImage;
            _pinImage = TrayIconFactory.CreatePinImage(LogicalToDeviceUnits(ToolbarPinSize));
            _pin.Image = _pinImage;
            previousPin.Dispose();
            foreach (Image image in _toolbarImages) image.Dispose();
            _toolbarImages.Clear();
            foreach (var pair in new[] { (_copyButton, MenuActionIcon.Copy), (_saveButton, MenuActionIcon.Save), (_closeButton, MenuActionIcon.Close) })
            {
                Bitmap image = MenuIconFactory.Create(pair.Item2, LogicalToDeviceUnits(UiTheme.IconSize));
                _toolbarImages.Add(image);
                pair.Item1.Image = image;
            }
            _iconDpi = DeviceDpi;
        }
    }

    private int MinimumChromeWidth()
    {
        int pad = LogicalToDeviceUnits(UiTheme.Spacing);
        int row = Math.Max(LogicalToDeviceUnits(UiTheme.ButtonHeight), Font.Height + pad * 2);
        int action = TextRenderer.MeasureText("保存", Font).Width + LogicalToDeviceUnits(UiTheme.IconSize) + pad * 2;
        return row * 2 + TextRenderer.MeasureText("1000%", Font).Width + action * 2 + pad * 6 + 2;
    }

    internal void SetZoom(double zoom)
    {
        if (!double.IsFinite(zoom)) return;
        Rectangle workArea = Screen.FromControl(this).WorkingArea;
        double maxZoom = Math.Min(8, Math.Min((workArea.Width - 4.0) / _image.Width,
            (workArea.Height - _chromeHeight - 4.0) / _image.Height));
        // Extremely large captures still need to fit on the selected monitor.
        double minZoom = Math.Min(0.1, maxZoom);
        _zoom = Math.Clamp(zoom, minZoom, Math.Max(minZoom, maxZoom));
        _displaySize = new Size(Math.Max(1, (int)Math.Round(_image.Width * _zoom)),
            Math.Max(1, (int)Math.Round(_image.Height * _zoom)));
        ClientSize = new Size(Math.Max(MinimumChromeWidth(), _displaySize.Width + 2),
            _displaySize.Height + _chromeHeight + 2);
        _zoomButton.Text = $"{_zoom * 100:0}%";
        _toolTip.SetToolTip(_zoomButton, $"缩放 {_zoom * 100:0.#}% · 点击恢复原始大小");
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
        PaintWindowFrame(e.Graphics);
        using var imageBorder = new Pen(UiTheme.Border);
        Rectangle bounds = ImageRectangle;
        e.Graphics.DrawRectangle(imageBorder, bounds.Left - 1, bounds.Top - 1, bounds.Width + 1, bounds.Height + 1);
    }

    private void PaintHeader(Graphics graphics)
    {
        var state = graphics.Save();
        graphics.SetClip(new Rectangle(0, 0, ClientSize.Width, _chromeHeight + 1), CombineMode.Intersect);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var surface = new SolidBrush(UiTheme.Surface);
        graphics.FillRoundedRectangle(surface, 0, 0, Width, Height,
            UiTheme.Scale(UiTheme.CornerRadius, DeviceDpi));
        graphics.Restore(state);
    }

    private void PaintWindowFrame(Graphics graphics)
    {
        using var border = new Pen(ContainsFocus ? UiTheme.Accent : UiTheme.Border);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, Width - 1, Height - 1,
            UiTheme.Scale(UiTheme.CornerRadius, DeviceDpi));
    }

    protected override void OnActivated(EventArgs e) { base.OnActivated(e); Invalidate(true); }
    protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); Invalidate(true); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        DragImage(this, e);
    }

    private void DragImage(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        // Native window dragging keeps the screenshot and its controls in one moving surface.
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN / HTCAPTION
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
            Clipboard.SetImage(_image);
            ShowFeedback("贴图已复制，可粘贴到其他应用。");
        }
        catch (ExternalException)
        {
            ShowFeedback("复制失败：剪贴板正被占用，请稍后重试。", error: true);
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
            ShowFeedback("已保存为 PNG 图片。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException)
        {
            ShowFeedback($"保存失败：{ex.Message}", error: true);
        }
    }

    private void ShowFeedback(string text, bool error = false)
    {
        _identity.Text = text;
        _identity.ForeColor = error ? UiTheme.Error : UiTheme.Accent;
        _toolTip.SetToolTip(_identity, text);
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
            // Repaint this fragment of the same window frame. Keep BackColor
            // opaque so rounded button corners also use the white surface.
            e.Graphics.Clear(_window.BackColor);
            var state = e.Graphics.Save();
            e.Graphics.TranslateTransform(-Left, -Top);
            _window.PaintHeader(e.Graphics);
            _window.PaintWindowFrame(e.Graphics);
            e.Graphics.Restore(state);
        }
    }
}
