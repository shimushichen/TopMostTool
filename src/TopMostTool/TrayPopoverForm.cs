using System.Drawing.Drawing2D;

namespace TopMostTool;

internal sealed record TrayPopoverSnapshot(
    IReadOnlyList<ManagedWindow> ManagedWindows,
    int ScreenshotCount,
    bool StartWithWindows,
    HotKeyBinding WindowShortcut,
    HotKeyBinding ScreenshotShortcut,
    string? ErrorTitle,
    string? ErrorDetail,
    bool HasErrorAction);

internal sealed class TrayPopoverForm : Form
{
    private const int MaxVisibleWindowRows = 3;

    private readonly Action _captureAction;
    private readonly Action _selectWindowAction;
    private readonly Action<IntPtr> _unpinWindowAction;
    private readonly Action _unpinAllAction;
    private readonly Action _closeAllScreenshotsAction;
    private readonly Action _shortcutSettingsAction;
    private readonly Action _toggleStartupAction;
    private readonly Action _aboutAction;
    private readonly Action _exitAction;
    private readonly Action _errorAction;

    private readonly ToolTip _toolTip = new();
    private readonly HeaderBar _header;
    private readonly ActionTile _captureTile;
    private readonly ActionTile _selectTile;
    private readonly ErrorBanner _errorBanner;
    private readonly GroupedCard _manageCard;
    private readonly SectionHeaderRow _pinnedHeader;
    private readonly SmoothScrollList _windowsList;
    private readonly EmptyHintRow _emptyWindowsRow;
    private readonly ScreenshotsRow _screenshotsRow;
    private readonly GroupedCard _settingsCard;
    private readonly NavRow _shortcutRow;
    private readonly ToggleRow _startupRow;
    private readonly FooterButton _aboutButton;
    private readonly FooterButton _exitButton;

    private TrayPopoverSnapshot? _snapshot;
    private Rectangle _anchorWorkArea;
    private long _lastDeactivatedTick;
    private bool _useNativeDwmCorners;
    private bool _suppressAutoHide;
    private bool _layingOut;

    internal TrayPopoverForm(
        Action captureAction,
        Action selectWindowAction,
        Action<IntPtr> unpinWindowAction,
        Action unpinAllAction,
        Action closeAllScreenshotsAction,
        Action shortcutSettingsAction,
        Action toggleStartupAction,
        Action aboutAction,
        Action exitAction,
        Action errorAction)
    {
        _captureAction = captureAction;
        _selectWindowAction = selectWindowAction;
        _unpinWindowAction = unpinWindowAction;
        _unpinAllAction = unpinAllAction;
        _closeAllScreenshotsAction = closeAllScreenshotsAction;
        _shortcutSettingsAction = shortcutSettingsAction;
        _toggleStartupAction = toggleStartupAction;
        _aboutAction = aboutAction;
        _exitAction = exitAction;
        _errorAction = errorAction;

        Text = "窗口置顶";
        AccessibleName = "窗口置顶控制面板";
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

        _header = new HeaderBar();
        _captureTile = new ActionTile(UiIcon.Screenshot, "截图贴图");
        _captureTile.Click += (_, _) => RunAndHide(_captureAction);

        _selectTile = new ActionTile(UiIcon.SelectWindow, "选择窗口");
        _selectTile.Click += (_, _) => RunAndHide(_selectWindowAction);

        _errorBanner = new ErrorBanner(_toolTip) { Visible = false };
        _errorBanner.Click += (_, _) =>
        {
            if (_snapshot?.HasErrorAction == true) RunAndHide(_errorAction);
        };

        _manageCard = new GroupedCard();
        _pinnedHeader = new SectionHeaderRow("已置顶窗口", "全部取消");
        _pinnedHeader.ActionClicked += (_, _) => _unpinAllAction();

        _windowsList = new SmoothScrollList();
        _emptyWindowsRow = new EmptyHintRow("暂无置顶窗口");
        _screenshotsRow = new ScreenshotsRow();
        _screenshotsRow.CloseAllClicked += (_, _) => _closeAllScreenshotsAction();
        _manageCard.Controls.AddRange(new Control[] { _pinnedHeader, _windowsList, _emptyWindowsRow, _screenshotsRow });

        _settingsCard = new GroupedCard();
        _shortcutRow = new NavRow(UiIcon.Settings, "快捷键设置");
        _shortcutRow.Click += (_, _) => RunAndHide(_shortcutSettingsAction);
        _startupRow = new ToggleRow(UiIcon.Startup, "开机启动");
        _startupRow.Click += (_, _) => _toggleStartupAction();
        _settingsCard.Controls.AddRange(new Control[] { _shortcutRow, _startupRow });

        _aboutButton = new FooterButton(UiIcon.About, $"关于 · {UiTheme.AppVersionLabel}", danger: false);
        _aboutButton.Click += (_, _) => RunAndHide(_aboutAction);

        _exitButton = new FooterButton(UiIcon.Exit, "退出", danger: true);
        _exitButton.Click += (_, _) => RunAndHide(_exitAction);

        Controls.AddRange(new Control[]
        {
            _header, _captureTile, _selectTile, _errorBanner,
            _manageCard, _settingsCard, _aboutButton, _exitButton
        });

        UiTheme.ApplyRoundedCorners(this, 12);
        HandleCreated += (_, _) => ApplyPopoverChrome();
        DpiChangedAfterParent += (_, _) => ApplyPopoverChrome();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= (int)NativeMethods.WsExToolWindow;
            return cp;
        }
    }

    private void ApplyPopoverChrome()
    {
        if (!IsHandleCreated || SystemInformation.HighContrast) return;
        int corners = NativeMethods.DwmWindowCornerRound;
        _useNativeDwmCorners = NativeMethods.DwmSetWindowAttribute(Handle,
            NativeMethods.DwmwaWindowCornerPreference, ref corners, sizeof(int)) == 0;
        int border = UiTheme.Border.R | (UiTheme.Border.G << 8) | (UiTheme.Border.B << 16);
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmwaBorderColor, ref border, sizeof(int));
        Invalidate();
    }

    internal void ToggleAtTray(Point cursorPosition, TrayPopoverSnapshot snapshot)
    {
        if (Visible)
        {
            Hide();
            return;
        }
        if (Environment.TickCount64 - _lastDeactivatedTick < 220) return;
        ShowAtTray(cursorPosition, snapshot);
    }

    internal void ShowAtTray(Point cursorPosition, TrayPopoverSnapshot snapshot)
    {
        _anchorWorkArea = Screen.FromPoint(cursorPosition).WorkingArea;
        if (_anchorWorkArea.Width <= 0 || _anchorWorkArea.Height <= 0)
            _anchorWorkArea = Screen.PrimaryScreen!.WorkingArea;
        UpdateSnapshot(snapshot);
        AnchorBottomRight();
        Show();
        NativeMethods.SetForegroundWindow(Handle);
        Activate();
    }

    internal void UpdateSnapshot(TrayPopoverSnapshot snapshot)
    {
        _snapshot = snapshot;
        _header.SetCounts(snapshot.ManagedWindows.Count, snapshot.ScreenshotCount);
        _captureTile.SetSubtitle(snapshot.ScreenshotShortcut.ToString());
        _selectTile.SetSubtitle(snapshot.WindowShortcut.ToString());

        bool hasError = !string.IsNullOrWhiteSpace(snapshot.ErrorTitle);
        _errorBanner.Visible = hasError;
        if (hasError)
            _errorBanner.UpdateError(snapshot.ErrorTitle!, snapshot.ErrorDetail ?? string.Empty, snapshot.HasErrorAction);

        string sectionTitle = snapshot.ManagedWindows.Count > MaxVisibleWindowRows
            ? $"已置顶窗口 · {snapshot.ManagedWindows.Count} 个（滚轮翻阅）"
            : "已置顶窗口";
        _pinnedHeader.UpdateHeader(sectionTitle, snapshot.ManagedWindows.Count > 0);
        RebuildWindowRows(snapshot.ManagedWindows);
        _screenshotsRow.SetCount(snapshot.ScreenshotCount);
        _startupRow.SetChecked(snapshot.StartWithWindows);

        LayoutPopover();
        if (Visible) AnchorBottomRight();
        Invalidate(true);
    }

    private void RebuildWindowRows(IReadOnlyList<ManagedWindow> windows)
    {
        var rows = new List<PinnedWindowRow>(windows.Count);
        foreach (ManagedWindow window in windows)
        {
            var row = new PinnedWindowRow(window, _toolTip);
            IntPtr handle = window.Handle;
            row.UnpinRequested += (_, _) => _unpinWindowAction(handle);
            rows.Add(row);
        }
        _windowsList.SetRows(rows);
    }

    private int Scaled(int value) => UiTheme.Scale(value, DeviceDpi);

    private void LayoutPopover()
    {
        if (_layingOut || IsDisposed) return;
        _layingOut = true;
        try
        {
            int width = Scaled(308);
            int pad = Scaled(14);
            int contentWidth = width - pad * 2;
            int y = Scaled(14);

            int headerH = Scaled(24);
            _header.SetBounds(pad, y, contentWidth, headerH);
            y += headerH + Scaled(12);

            int tileGap = Scaled(10);
            int tileW = (contentWidth - tileGap) / 2;
            int tileH = Scaled(86);
            _captureTile.SetBounds(pad, y, tileW, tileH);
            _selectTile.SetBounds(pad + tileW + tileGap, y, contentWidth - tileW - tileGap, tileH);
            y += tileH + Scaled(10);

            if (_errorBanner.Visible)
            {
                int errH = Scaled(36);
                _errorBanner.SetBounds(pad, y, contentWidth, errH);
                y += errH + Scaled(10);
            }

            // Management grouped card: Pinned Windows (strictly capped at 3 visible rows) + Screenshots row
            int cardPadY = Scaled(6);
            int childInsetX = Scaled(3);
            int childW = contentWidth - childInsetX * 2;
            int innerY = cardPadY;
            int headerRowH = Scaled(26);
            _pinnedHeader.SetBounds(childInsetX, innerY, childW, headerRowH);
            innerY += headerRowH;

            int windowCount = _windowsList.RowCount;
            if (windowCount == 0)
            {
                _windowsList.Visible = false;
                _emptyWindowsRow.Visible = true;
                int emptyH = Scaled(28);
                _emptyWindowsRow.SetBounds(childInsetX, innerY, childW, emptyH);
                innerY += emptyH;
            }
            else
            {
                _emptyWindowsRow.Visible = false;
                _windowsList.Visible = true;
                int rowH = Scaled(32);
                int listH = Math.Min(windowCount, MaxVisibleWindowRows) * rowH;
                _windowsList.Configure(rowH, childW, listH);
                _windowsList.SetBounds(childInsetX, innerY, childW, listH);
                innerY += listH;
            }

            innerY += Scaled(4);
            int dividerY = innerY;
            innerY += Scaled(5);
            int shotsH = Scaled(36);
            _screenshotsRow.SetBounds(childInsetX, innerY, childW, shotsH);
            innerY += shotsH + cardPadY;

            _manageCard.SetDividerY(dividerY);
            _manageCard.SetBounds(pad, y, contentWidth, innerY);
            y += innerY + Scaled(10);

            // Settings grouped card: Shortcuts + inset divider + Launch at Startup
            int setRowH = Scaled(36);
            int setDividerY = cardPadY + setRowH;
            _shortcutRow.SetBounds(childInsetX, cardPadY, childW, setRowH);
            _startupRow.SetBounds(childInsetX, setDividerY + 1, childW, setRowH);
            int settingsH = cardPadY + setRowH + 1 + setRowH + cardPadY;
            _settingsCard.SetDividerY(setDividerY);
            _settingsCard.SetBounds(pad, y, contentWidth, settingsH);
            y += settingsH + Scaled(10);

            // Minimal Footer Bar directly on the #F5F5F7 canvas
            int footerH = Scaled(28);
            int aboutW = _aboutButton.PreferredWidth;
            int exitW = _exitButton.PreferredWidth;
            _aboutButton.SetBounds(pad, y, aboutW, footerH);
            _exitButton.SetBounds(width - pad - exitW, y, exitW, footerH);
            y += footerH + Scaled(12);

            ClientSize = new Size(width, y);
        }
        finally { _layingOut = false; }
    }

    // Always anchor cleanly to the bottom-right corner above the taskbar (like Windows 11 Quick Settings).
    private void AnchorBottomRight()
    {
        Rectangle workArea = _anchorWorkArea.Width > 0 && _anchorWorkArea.Height > 0
            ? _anchorWorkArea
            : Screen.FromControl(this).WorkingArea;
        int margin = Scaled(12);
        int x = Math.Max(workArea.Left + margin, workArea.Right - Width - margin);
        int y = Math.Max(workArea.Top + margin, workArea.Bottom - Height - margin);
        Location = new Point(x, y);
    }

    private void RunAndHide(Action action)
    {
        _suppressAutoHide = true;
        try
        {
            Hide();
            action();
        }
        finally { _suppressAutoHide = false; }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (_suppressAutoHide || !Visible) return;
        _lastDeactivatedTick = Environment.TickCount64;
        Hide();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            Hide();
        }
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        LayoutPopover();
        if (Visible) AnchorBottomRight();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_useNativeDwmCorners) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(UiTheme.Border);
        e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, Width - 1, Height - 1, Scaled(8));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _toolTip.Dispose();
        base.Dispose(disposing);
    }

    // ─── Sub-controls ────────────────────────────────────────────────────────────

    // Apple-style smooth wheel-scrollable list with an ultra-slim floating pill scroll thumb.
    private sealed class SmoothScrollList : Control
    {
        private readonly List<PinnedWindowRow> _rows = new();
        private int _rowHeight = 32;
        private int _scrollOffset;
        private int _contentHeight;
        private bool _draggingThumb;
        private int _dragStartY;
        private int _dragStartOffset;
        private Rectangle _thumbRect;

        internal int RowCount => _rows.Count;

        internal SmoothScrollList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
        }

        internal void SetRows(List<PinnedWindowRow> rows)
        {
            SuspendLayout();
            foreach (PinnedWindowRow old in _rows)
            {
                Controls.Remove(old);
                old.Dispose();
            }
            _rows.Clear();
            foreach (PinnedWindowRow row in rows)
            {
                row.MouseWheel += (_, e) => ScrollByDelta(e.Delta);
                _rows.Add(row);
                Controls.Add(row);
            }
            ResumeLayout(false);
        }

        internal void Configure(int rowHeight, int width, int viewportHeight)
        {
            _rowHeight = Math.Max(1, rowHeight);
            _contentHeight = _rows.Count * _rowHeight;
            int maxOffset = Math.Max(0, _contentHeight - viewportHeight);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxOffset);
            PositionRows(width, viewportHeight);
            Invalidate();
        }

        private void PositionRows(int width, int viewportHeight)
        {
            bool hasScroll = _contentHeight > viewportHeight;
            int thumbReserve = hasScroll ? UiTheme.Scale(9, DeviceDpi) : 0;
            int rowW = Math.Max(1, width - thumbReserve);
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetBounds(0, i * _rowHeight - _scrollOffset, rowW, _rowHeight);
        }

        private void ScrollByDelta(int wheelDelta)
        {
            int maxOffset = Math.Max(0, _contentHeight - Height);
            if (maxOffset <= 0) return;
            int step = Math.Max(_rowHeight / 2, (int)Math.Round(_rowHeight * Math.Abs(wheelDelta) / 120.0));
            int next = Math.Clamp(_scrollOffset + (wheelDelta < 0 ? step : -step), 0, maxOffset);
            if (next == _scrollOffset) return;
            _scrollOffset = next;
            PositionRows(Width, Height);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollByDelta(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_thumbRect.IsEmpty && _thumbRect.Contains(e.Location))
            {
                _draggingThumb = true;
                _dragStartY = e.Y;
                _dragStartOffset = _scrollOffset;
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_draggingThumb)
            {
                int maxOffset = Math.Max(0, _contentHeight - Height);
                int trackH = Math.Max(1, Height - _thumbRect.Height - UiTheme.Scale(6, DeviceDpi));
                int deltaY = e.Y - _dragStartY;
                int next = Math.Clamp(_dragStartOffset + (int)Math.Round(deltaY * (double)maxOffset / trackH), 0, maxOffset);
                if (next != _scrollOffset)
                {
                    _scrollOffset = next;
                    PositionRows(Width, Height);
                    Invalidate();
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_draggingThumb)
            {
                _draggingThumb = false;
                Capture = false;
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Surface);
            if (_contentHeight <= Height || Height <= 4)
            {
                _thumbRect = Rectangle.Empty;
                return;
            }
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            int thumbW = Math.Max(3, UiTheme.Scale(4, dpi));
            int padY = UiTheme.Scale(3, dpi);
            int trackH = Math.Max(1, Height - padY * 2);
            int thumbH = Math.Max(UiTheme.Scale(18, dpi), (int)Math.Round(trackH * (double)Height / _contentHeight));
            int maxOffset = Math.Max(1, _contentHeight - Height);
            int thumbY = padY + (int)Math.Round((trackH - thumbH) * (_scrollOffset / (double)maxOffset));
            int thumbX = Width - thumbW - UiTheme.Scale(3, dpi);
            _thumbRect = new Rectangle(thumbX - 2, thumbY, thumbW + 4, thumbH);

            Color thumbColor = _draggingThumb ? UiTheme.MutedText : Color.FromArgb(120, 142, 142, 147);
            using var brush = new SolidBrush(thumbColor);
            e.Graphics.FillRoundedRectangle(brush, thumbX, thumbY, thumbW, thumbH, thumbW / 2F);
        }
    }

    private sealed class GroupedCard : Panel
    {
        private int _dividerY = -1;

        internal GroupedCard()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = UiTheme.Surface;
        }

        internal void SetDividerY(int y)
        {
            _dividerY = y;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Background);
            if (Width <= 2 || Height <= 2) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int radius = UiTheme.Scale(11, DeviceDpi);
            using (var fill = new SolidBrush(UiTheme.Surface))
                e.Graphics.FillRoundedRectangle(fill, 0.5F, 0.5F, Width - 1, Height - 1, radius);
            if (_dividerY > 0 && _dividerY < Height - 2)
            {
                int inset = UiTheme.Scale(12, DeviceDpi);
                using var divider = new Pen(UiTheme.BorderSubtle);
                e.Graphics.DrawLine(divider, inset, _dividerY, Width - inset, _dividerY);
            }
            using (var border = new Pen(UiTheme.BorderSubtle))
                e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, Width - 1, Height - 1, radius);
        }
    }

    private sealed class HeaderBar : Control
    {
        private int _pinnedCount;
        private int _screenshotCount;
        private Bitmap? _pinBitmap;
        private int _pinDpi;

        internal HeaderBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Background;
        }

        internal void SetCounts(int pinned, int screenshots)
        {
            _pinnedCount = pinned;
            _screenshotCount = screenshots;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            if (_pinBitmap is null || _pinDpi != dpi)
            {
                _pinBitmap?.Dispose();
                _pinBitmap = TrayIconFactory.CreateMenuPinImage(UiTheme.Scale(18, dpi));
                _pinDpi = dpi;
            }

            int pinY = (Height - _pinBitmap.Height) / 2;
            e.Graphics.DrawImageUnscaled(_pinBitmap, 0, pinY);

            int titleX = _pinBitmap.Width + UiTheme.Scale(7, dpi);
            TextRenderer.DrawText(e.Graphics, "窗口置顶", UiTheme.MenuFont,
                new Rectangle(titleX, 0, Width / 2, Height), UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int gap = UiTheme.Scale(5, dpi);
            int rightX = Width;
            rightX = DrawCountPill(e.Graphics, $"贴图 {_screenshotCount}", _screenshotCount > 0, rightX, dpi) - gap;
            DrawCountPill(e.Graphics, $"置顶 {_pinnedCount}", _pinnedCount > 0, rightX, dpi);
        }

        private int DrawCountPill(Graphics graphics, string text, bool active, int rightEdge, int dpi)
        {
            using var smallFont = new Font(UiTheme.MenuFont.FontFamily, 8F);
            Size textSize = TextRenderer.MeasureText(graphics, text, smallFont, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int padX = UiTheme.Scale(7, dpi);
            int pillW = textSize.Width + padX * 2;
            int pillH = Math.Min(Height, textSize.Height + UiTheme.Scale(5, dpi));
            int x = rightEdge - pillW;
            int y = (Height - pillH) / 2;
            Color bg = active ? UiTheme.Accent : UiTheme.SurfaceMuted;
            Color fg = active ? UiTheme.AccentText : UiTheme.MutedText;
            using (var brush = new SolidBrush(bg))
                graphics.FillRoundedRectangle(brush, x, y, pillW, pillH, pillH / 2F);
            TextRenderer.DrawText(graphics, text, smallFont, new Rectangle(x, y, pillW, pillH), fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            return x;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _pinBitmap?.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class ActionTile : Control
    {
        private readonly UiIcon _icon;
        private readonly string _title;
        private string _subtitle = string.Empty;
        private Bitmap? _iconBitmap;
        private int _iconDpi;
        private bool _hovered;
        private bool _pressed;

        internal ActionTile(UiIcon icon, string title)
        {
            _icon = icon;
            _title = title;
            AccessibleName = title;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        internal void SetSubtitle(string subtitle)
        {
            if (_subtitle == subtitle) return;
            _subtitle = subtitle;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Background);
            if (Width <= 2 || Height <= 2) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            int radius = UiTheme.Scale(12, dpi);
            Color fill = _pressed ? UiTheme.Pressed : _hovered ? UiTheme.SurfaceSubtle : UiTheme.Surface;
            Color border = _hovered ? Color.FromArgb(198, 198, 204) : UiTheme.BorderSubtle;
            using (var fillBrush = new SolidBrush(fill))
                e.Graphics.FillRoundedRectangle(fillBrush, 0.5F, 0.5F, Width - 1, Height - 1, radius);
            using (var borderPen = new Pen(border))
                e.Graphics.DrawRoundedRectangle(borderPen, 0.5F, 0.5F, Width - 1, Height - 1, radius);

            int iconSize = UiTheme.Scale(19, dpi);
            if (_iconBitmap is null || _iconDpi != dpi)
            {
                _iconBitmap?.Dispose();
                _iconBitmap = UiIconFactory.Create(_icon, iconSize, UiTheme.Text);
                _iconDpi = dpi;
            }

            int wellSize = UiTheme.Scale(32, dpi);
            int wellX = (Width - wellSize) / 2;
            int wellY = UiTheme.Scale(11, dpi);
            Color wellColor = _hovered ? UiTheme.Hover : UiTheme.Background;
            using (var wellBrush = new SolidBrush(wellColor))
                e.Graphics.FillRoundedRectangle(wellBrush, wellX, wellY, wellSize, wellSize, UiTheme.Scale(8, dpi));
            e.Graphics.DrawImageUnscaled(_iconBitmap,
                wellX + (wellSize - _iconBitmap.Width) / 2,
                wellY + (wellSize - _iconBitmap.Height) / 2);

            int titleY = wellY + wellSize + UiTheme.Scale(6, dpi);
            int titleH = UiTheme.MenuFont.Height + UiTheme.Scale(2, dpi);
            TextRenderer.DrawText(e.Graphics, _title, UiTheme.MenuFont,
                new Rectangle(UiTheme.Scale(6, dpi), titleY, Width - UiTheme.Scale(12, dpi), titleH),
                UiTheme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            if (!string.IsNullOrEmpty(_subtitle))
            {
                using var subFont = new Font(UiTheme.MenuFont.FontFamily, 7.6F);
                int subY = titleY + titleH + UiTheme.Scale(1, dpi);
                int subH = Math.Max(1, Height - subY - UiTheme.Scale(6, dpi));
                TextRenderer.DrawText(e.Graphics, _subtitle, subFont,
                    new Rectangle(UiTheme.Scale(6, dpi), subY, Width - UiTheme.Scale(12, dpi), subH),
                    UiTheme.MutedText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode is Keys.Enter or Keys.Space) OnClick(EventArgs.Empty); base.OnKeyDown(e); }
        protected override void Dispose(bool disposing) { if (disposing) _iconBitmap?.Dispose(); base.Dispose(disposing); }
    }

    private sealed class ErrorBanner : Control
    {
        private readonly ToolTip _toolTip;
        private string _title = string.Empty;
        private bool _clickable;
        private bool _hovered;
        private Bitmap? _iconBitmap;
        private int _iconDpi;

        internal ErrorBanner(ToolTip toolTip)
        {
            _toolTip = toolTip;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        internal void UpdateError(string title, string detail, bool clickable)
        {
            _title = title;
            _clickable = clickable;
            Cursor = clickable ? Cursors.Hand : Cursors.Default;
            AccessibleName = string.IsNullOrEmpty(detail) ? title : $"{title}：{detail}";
            _toolTip.SetToolTip(this, detail);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Background);
            if (Width <= 2 || Height <= 2) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            int radius = UiTheme.Scale(10, dpi);
            using (var fill = new SolidBrush(_hovered && _clickable ? UiTheme.DangerHover : UiTheme.ErrorSoft))
                e.Graphics.FillRoundedRectangle(fill, 0.5F, 0.5F, Width - 1, Height - 1, radius);
            using (var border = new Pen(UiTheme.ErrorBorder))
                e.Graphics.DrawRoundedRectangle(border, 0.5F, 0.5F, Width - 1, Height - 1, radius);

            int iconSize = UiTheme.Scale(16, dpi);
            if (_iconBitmap is null || _iconDpi != dpi)
            {
                _iconBitmap?.Dispose();
                _iconBitmap = UiIconFactory.Create(UiIcon.Error, iconSize, UiTheme.Error);
                _iconDpi = dpi;
            }
            int leftPad = UiTheme.Scale(10, dpi);
            e.Graphics.DrawImageUnscaled(_iconBitmap, leftPad, (Height - _iconBitmap.Height) / 2);

            int textX = leftPad + _iconBitmap.Width + UiTheme.Scale(7, dpi);
            int rightReserve = _clickable ? UiTheme.Scale(22, dpi) : leftPad;
            TextRenderer.DrawText(e.Graphics, _title, UiTheme.CaptionFont,
                new Rectangle(textX, 0, Math.Max(1, Width - textX - rightReserve), Height),
                UiTheme.Error,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (_clickable)
                DrawChevron(e.Graphics, Width - UiTheme.Scale(12, dpi), Height / 2, UiTheme.Error, dpi);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void Dispose(bool disposing) { if (disposing) _iconBitmap?.Dispose(); base.Dispose(disposing); }
    }

    private sealed class SectionHeaderRow : Control
    {
        private string _title;
        private readonly string _actionText;
        private bool _actionEnabled;
        private bool _actionHovered;
        private Rectangle _actionBounds;

        internal event EventHandler? ActionClicked;

        internal SectionHeaderRow(string title, string actionText)
        {
            _title = title;
            _actionText = actionText;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
        }

        internal void UpdateHeader(string title, bool enabled)
        {
            _title = title;
            _actionEnabled = enabled;
            if (!enabled) _actionHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Surface);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            int padX = UiTheme.Scale(10, dpi);
            using var smallFont = new Font(UiTheme.MenuFont.FontFamily, 8F);
            int rightReserve = _actionEnabled ? UiTheme.Scale(64, dpi) : padX;
            TextRenderer.DrawText(e.Graphics, _title, smallFont,
                new Rectangle(padX, 0, Math.Max(1, Width - padX - rightReserve), Height), UiTheme.MutedText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            if (_actionEnabled)
            {
                Size size = TextRenderer.MeasureText(e.Graphics, _actionText, smallFont, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                int pillPadX = UiTheme.Scale(6, dpi);
                int pillW = size.Width + pillPadX * 2;
                int pillH = Math.Min(Height - UiTheme.Scale(4, dpi), size.Height + UiTheme.Scale(4, dpi));
                _actionBounds = new Rectangle(Width - padX - pillW, (Height - pillH) / 2, pillW, pillH);
                if (_actionHovered)
                {
                    using var pill = new SolidBrush(UiTheme.Hover);
                    e.Graphics.FillRoundedRectangle(pill, _actionBounds.X, _actionBounds.Y,
                        _actionBounds.Width, _actionBounds.Height, UiTheme.Scale(5, dpi));
                }
                TextRenderer.DrawText(e.Graphics, _actionText, smallFont, _actionBounds,
                    _actionHovered ? UiTheme.Text : UiTheme.MutedText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
            else
            {
                _actionBounds = Rectangle.Empty;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = _actionEnabled && _actionBounds.Contains(e.Location);
            if (over != _actionHovered)
            {
                _actionHovered = over;
                Cursor = over ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _actionHovered = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _actionEnabled && _actionBounds.Contains(e.Location))
                ActionClicked?.Invoke(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }
    }

    private sealed class EmptyHintRow : Control
    {
        private readonly string _text;

        internal EmptyHintRow(string text)
        {
            _text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Surface);
            int padX = UiTheme.Scale(10, DeviceDpi);
            TextRenderer.DrawText(e.Graphics, _text, UiTheme.CaptionFont,
                new Rectangle(padX, 0, Math.Max(1, Width - padX * 2), Height), UiTheme.DisabledText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    private sealed class PinnedWindowRow : Control
    {
        private readonly string _title;
        private readonly ToolTip _toolTip;
        private bool _hovered;
        private bool _pressed;

        internal event EventHandler? UnpinRequested;

        internal PinnedWindowRow(ManagedWindow window, ToolTip toolTip)
        {
            _title = window.Title;
            _toolTip = toolTip;
            AccessibleName = "取消置顶：" + window.Title;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            int available = Math.Max(1, Width - UiTheme.Scale(58, DeviceDpi));
            int measured = TextRenderer.MeasureText(_title, UiTheme.CaptionFont, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            _toolTip.SetToolTip(this, measured > available ? _title : string.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Surface);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            int insetX = UiTheme.Scale(4, dpi);
            if (_hovered || _pressed)
            {
                using var bg = new SolidBrush(_pressed ? UiTheme.Pressed : UiTheme.Hover);
                e.Graphics.FillRoundedRectangle(bg, insetX, 1, Width - insetX * 2, Height - 2, UiTheme.Scale(7, dpi));
            }

            int dotSize = UiTheme.Scale(5, dpi);
            int dotX = UiTheme.Scale(12, dpi);
            using (var dot = new SolidBrush(UiTheme.Text))
                e.Graphics.FillEllipse(dot, dotX, (Height - dotSize) / 2, dotSize, dotSize);

            int closeArea = UiTheme.Scale(26, dpi);
            int textX = dotX + dotSize + UiTheme.Scale(9, dpi);
            TextRenderer.DrawText(e.Graphics, _title, UiTheme.CaptionFont,
                new Rectangle(textX, 0, Math.Max(1, Width - textX - closeArea - UiTheme.Scale(4, dpi)), Height),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            int cx = Width - UiTheme.Scale(15, dpi);
            int cy = Height / 2;
            float r = UiTheme.Scale(3, dpi);
            Color crossColor = _hovered ? UiTheme.Text : UiTheme.DisabledText;
            using var pen = new Pen(crossColor, 1.4F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
            e.Graphics.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) UnpinRequested?.Invoke(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }
    }

    private sealed class ScreenshotsRow : Control
    {
        private int _count;
        private bool _actionHovered;
        private Rectangle _actionBounds;
        private Bitmap? _iconBitmap;
        private int _iconDpi;

        internal event EventHandler? CloseAllClicked;

        internal ScreenshotsRow()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
        }

        internal void SetCount(int count)
        {
            _count = count;
            if (count == 0) _actionHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Surface);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            int iconSize = UiTheme.Scale(17, dpi);
            if (_iconBitmap is null || _iconDpi != dpi)
            {
                _iconBitmap?.Dispose();
                _iconBitmap = UiIconFactory.Create(UiIcon.CloseScreenshots, iconSize, UiTheme.Text);
                _iconDpi = dpi;
            }
            int padX = UiTheme.Scale(10, dpi);
            e.Graphics.DrawImageUnscaled(_iconBitmap, padX, (Height - _iconBitmap.Height) / 2);

            int textX = padX + _iconBitmap.Width + UiTheme.Scale(9, dpi);
            string label = $"截图贴图 · {_count} 张";
            TextRenderer.DrawText(e.Graphics, label, UiTheme.MenuFont,
                new Rectangle(textX, 0, Width / 2, Height), _count > 0 ? UiTheme.Text : UiTheme.MutedText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            using var smallFont = new Font(UiTheme.MenuFont.FontFamily, 8.2F);
            string btnText = "关闭全部";
            Size btnSize = TextRenderer.MeasureText(e.Graphics, btnText, smallFont, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int pillPadX = UiTheme.Scale(8, dpi);
            int pillW = btnSize.Width + pillPadX * 2;
            int pillH = Math.Min(Height - UiTheme.Scale(8, dpi), btnSize.Height + UiTheme.Scale(6, dpi));
            _actionBounds = new Rectangle(Width - padX - pillW, (Height - pillH) / 2, pillW, pillH);

            if (_count > 0)
            {
                Color pillBg = _actionHovered ? UiTheme.Pressed : UiTheme.Background;
                using (var fill = new SolidBrush(pillBg))
                    e.Graphics.FillRoundedRectangle(fill, _actionBounds.X, _actionBounds.Y,
                        _actionBounds.Width, _actionBounds.Height, UiTheme.Scale(6, dpi));
                TextRenderer.DrawText(e.Graphics, btnText, smallFont, _actionBounds, UiTheme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, btnText, smallFont, _actionBounds, UiTheme.DisabledText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = _count > 0 && _actionBounds.Contains(e.Location);
            if (over != _actionHovered)
            {
                _actionHovered = over;
                Cursor = over ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _actionHovered = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _count > 0 && _actionBounds.Contains(e.Location))
                CloseAllClicked?.Invoke(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }

        protected override void Dispose(bool disposing) { if (disposing) _iconBitmap?.Dispose(); base.Dispose(disposing); }
    }

    private sealed class NavRow : Control
    {
        private readonly UiIcon _icon;
        private readonly string _title;
        private Bitmap? _iconBitmap;
        private int _iconDpi;
        private bool _hovered;
        private bool _pressed;

        internal NavRow(UiIcon icon, string title)
        {
            _icon = icon;
            _title = title;
            AccessibleName = title;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Surface);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            int insetX = UiTheme.Scale(4, dpi);
            if (_hovered || _pressed)
            {
                using var bg = new SolidBrush(_pressed ? UiTheme.Pressed : UiTheme.Hover);
                e.Graphics.FillRoundedRectangle(bg, insetX, 2, Width - insetX * 2, Height - 4, UiTheme.Scale(7, dpi));
            }
            int iconSize = UiTheme.Scale(17, dpi);
            if (_iconBitmap is null || _iconDpi != dpi)
            {
                _iconBitmap?.Dispose();
                _iconBitmap = UiIconFactory.Create(_icon, iconSize, UiTheme.Text);
                _iconDpi = dpi;
            }
            int padX = UiTheme.Scale(10, dpi);
            e.Graphics.DrawImageUnscaled(_iconBitmap, padX, (Height - _iconBitmap.Height) / 2);

            int textX = padX + _iconBitmap.Width + UiTheme.Scale(9, dpi);
            TextRenderer.DrawText(e.Graphics, _title, UiTheme.MenuFont,
                new Rectangle(textX, 0, Math.Max(1, Width - textX - UiTheme.Scale(26, dpi)), Height),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            DrawChevron(e.Graphics, Width - UiTheme.Scale(13, dpi), Height / 2,
                _hovered ? UiTheme.Text : UiTheme.DisabledText, dpi);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void Dispose(bool disposing) { if (disposing) _iconBitmap?.Dispose(); base.Dispose(disposing); }
    }

    private sealed class ToggleRow : Control
    {
        private readonly UiIcon _icon;
        private readonly string _title;
        private Bitmap? _iconBitmap;
        private int _iconDpi;
        private bool _checked;
        private bool _hovered;
        private bool _pressed;

        internal ToggleRow(UiIcon icon, string title)
        {
            _icon = icon;
            _title = title;
            AccessibleName = title;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
        }

        internal void SetChecked(bool value)
        {
            _checked = value;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Surface);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            int insetX = UiTheme.Scale(4, dpi);
            if (_hovered || _pressed)
            {
                using var bg = new SolidBrush(_pressed ? UiTheme.Pressed : UiTheme.Hover);
                e.Graphics.FillRoundedRectangle(bg, insetX, 2, Width - insetX * 2, Height - 4, UiTheme.Scale(7, dpi));
            }
            int iconSize = UiTheme.Scale(17, dpi);
            if (_iconBitmap is null || _iconDpi != dpi)
            {
                _iconBitmap?.Dispose();
                _iconBitmap = UiIconFactory.Create(_icon, iconSize, UiTheme.Text);
                _iconDpi = dpi;
            }
            int padX = UiTheme.Scale(10, dpi);
            e.Graphics.DrawImageUnscaled(_iconBitmap, padX, (Height - _iconBitmap.Height) / 2);

            int switchW = UiTheme.Scale(34, dpi);
            int switchH = UiTheme.Scale(19, dpi);
            int switchX = Width - padX - switchW;
            int switchY = (Height - switchH) / 2;

            int textX = padX + _iconBitmap.Width + UiTheme.Scale(9, dpi);
            TextRenderer.DrawText(e.Graphics, _title, UiTheme.MenuFont,
                new Rectangle(textX, 0, Math.Max(1, switchX - textX - UiTheme.Scale(8, dpi)), Height),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Color trackColor = _checked ? UiTheme.Accent : Color.FromArgb(214, 214, 219);
            using (var track = new SolidBrush(trackColor))
                e.Graphics.FillRoundedRectangle(track, switchX, switchY, switchW, switchH, switchH / 2F);

            int thumbPad = Math.Max(2, UiTheme.Scale(2, dpi));
            int thumbSize = switchH - thumbPad * 2;
            int thumbX = _checked ? switchX + switchW - thumbSize - thumbPad : switchX + thumbPad;
            int thumbY = switchY + thumbPad;
            using (var thumbShadow = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                e.Graphics.FillEllipse(thumbShadow, thumbX, thumbY + 1, thumbSize, thumbSize);
            using (var thumb = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(thumb, thumbX, thumbY, thumbSize, thumbSize);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void Dispose(bool disposing) { if (disposing) _iconBitmap?.Dispose(); base.Dispose(disposing); }
    }

    private sealed class FooterButton : Control
    {
        private readonly UiIcon _icon;
        private readonly string _text;
        private readonly bool _danger;
        private Bitmap? _normalIcon;
        private Bitmap? _hoverIcon;
        private int _iconDpi;
        private bool _hovered;
        private bool _pressed;

        internal FooterButton(UiIcon icon, string text, bool danger)
        {
            _icon = icon;
            _text = text;
            _danger = danger;
            AccessibleName = text;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Background;
        }

        internal int PreferredWidth
        {
            get
            {
                int dpi = DeviceDpi;
                Size text = TextRenderer.MeasureText(_text, UiTheme.CaptionFont, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                return UiTheme.Scale(8, dpi) * 2 + UiTheme.Scale(15, dpi) + UiTheme.Scale(6, dpi) + text.Width + UiTheme.Scale(2, dpi);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dpi = DeviceDpi;
            if (_hovered || _pressed)
            {
                Color bg = _danger ? UiTheme.DangerHover : _pressed ? UiTheme.Pressed : UiTheme.SurfaceMuted;
                using var fill = new SolidBrush(bg);
                e.Graphics.FillRoundedRectangle(fill, 0, 0, Width, Height, UiTheme.Scale(7, dpi));
            }
            int iconSize = UiTheme.Scale(15, dpi);
            if (_normalIcon is null || _hoverIcon is null || _iconDpi != dpi)
            {
                _normalIcon?.Dispose();
                _hoverIcon?.Dispose();
                _normalIcon = UiIconFactory.Create(_icon, iconSize, UiTheme.MutedText);
                _hoverIcon = UiIconFactory.Create(_icon, iconSize, _danger ? UiTheme.Error : UiTheme.Text);
                _iconDpi = dpi;
            }
            int padX = UiTheme.Scale(8, dpi);
            Bitmap bmp = _hovered ? _hoverIcon : _normalIcon;
            e.Graphics.DrawImageUnscaled(bmp, padX, (Height - bmp.Height) / 2);

            Color fg = _hovered ? (_danger ? UiTheme.Error : UiTheme.Text) : UiTheme.MutedText;
            int textX = padX + bmp.Width + UiTheme.Scale(6, dpi);
            TextRenderer.DrawText(e.Graphics, _text, UiTheme.CaptionFont,
                new Rectangle(textX, 0, Math.Max(1, Width - textX - padX), Height), fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _normalIcon?.Dispose(); _hoverIcon?.Dispose(); }
            base.Dispose(disposing);
        }
    }

    private static void DrawChevron(Graphics graphics, int centerX, int centerY, Color color, int dpi)
    {
        using var pen = new Pen(color, 1.4F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        float size = UiTheme.Scale(3, dpi);
        graphics.DrawLines(pen, new[]
        {
            new PointF(centerX - size / 2F, centerY - size),
            new PointF(centerX + size / 2F, centerY),
            new PointF(centerX - size / 2F, centerY + size)
        });
    }
}
