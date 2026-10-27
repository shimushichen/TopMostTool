namespace TopMostTool;

internal enum MenuErrorKind
{
    General,
    WindowShortcut,
    ScreenshotShortcut,
    WindowPin,
    WindowUnpin,
    Screenshot,
    Startup
}


internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int ToggleHotKeyId = 1;
    private const int CancelHotKeyId = 2;
    private const int ScreenshotHotKeyId = 3;
    private const int PinMenuIconSize = 16;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _countItem;
    private readonly ToolStripMenuItem _managedWindowsMenu;
    private readonly ToolStripMenuItem _unpinAllWindowsMenu;
    private readonly ToolStripMenuItem _screenshotsMenu;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _captureItem;
    private readonly ToolStripMenuItem _feedbackItem = new() { Enabled = false, Visible = false };
    private Action? _feedbackAction;
    private Image _pinMenuImage;
    private readonly Dictionary<IntPtr, PinnedBadgeWindow> _badges = new();
    private readonly List<PinnedImageWindow> _screenshots = new();
    private readonly ConfigStore _configStore;
    private readonly WindowManager _windowManager = new();
    private readonly HotKeyMessageWindow _hotKeyWindow = new();
    private readonly WindowSelectionOverlay _selectionOverlay = new();
    private readonly System.Windows.Forms.Timer _foregroundTimer;
    private readonly WindowLocationMonitor _locationMonitor;
    private readonly SynchronizationContext _uiContext;
    private AppConfig _config;
    private IntPtr _lastExternalWindow;
    private bool _toggleHotKeyRegistered;
    private bool _cancelHotKeyRegistered;
    private bool _screenshotHotKeyRegistered;
    private bool _editingShortcuts;
    private bool _isExiting;
    private bool _capturePending;
    private int _screenshotNumber;
    private ScreenshotSelectionOverlay? _screenshotOverlay;
    private AboutForm? _aboutForm;

    internal TrayApplicationContext(ConfigStore? configStore = null)
    {
        _configStore = configStore ?? new ConfigStore();
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _config = _configStore.Load(out string? loadWarning);
        _config.StartWithWindows = StartupService.IsEnabled();
        _configStore.TrySave(_config, out _);
        _notifyIcon = new NotifyIcon
        {
            Text = "窗口置顶",
            Icon = TrayIconFactory.Create(),
            Visible = true
        };
        _pinMenuImage = CreatePinMenuImage(_selectionOverlay.DeviceDpi);

        _countItem = new ToolStripMenuItem { Enabled = false, Image = _pinMenuImage };
        _managedWindowsMenu = new ToolStripMenuItem("取消指定窗口置顶");
        _unpinAllWindowsMenu = new ToolStripMenuItem("取消全部窗口置顶")
        {
            ToolTipText = "取消本工具设置的全部窗口置顶"
        };
        _screenshotsMenu = new ToolStripMenuItem("关闭全部截图贴图")
        {
            ToolTipText = "关闭全部截图贴图；请先保存需要的图片"
        };
        _screenshotsMenu.Click += (_, _) => CloseAllScreenshots();
        _captureItem = new ToolStripMenuItem("截图贴图")
        {
            Image = _pinMenuImage,
            ShowShortcutKeys = false,
            ToolTipText = "拖动框选屏幕区域，松开生成贴图；右键或 Esc 取消"
        };
        _startupItem = new ToolStripMenuItem("开机启动")
        {
            Image = _config.StartWithWindows ? _pinMenuImage : null
        };

        _menu = BuildMenu();
        _notifyIcon.ContextMenuStrip = _menu;
        _notifyIcon.DoubleClick += (_, _) => ToggleLastExternalWindow();
        _notifyIcon.BalloonTipClicked += (_, _) => { };

        _hotKeyWindow.HotKeyPressed += OnHotKeyPressed;
        RegisterHotKeys(loadWarning);
        _selectionOverlay.WindowSelected += OnWindowSelected;

        _foregroundTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _foregroundTimer.Tick += (_, _) =>
        {
            RememberForegroundWindow();
            if (_badges.Count > 0)
            {
                SyncPinnedBadges();
            }
        };
        _foregroundTimer.Start();
        _locationMonitor = new WindowLocationMonitor();
        _locationMonitor.WindowLocationChanged += (_, hWnd) =>
            _uiContext.Post(_ => QueueBadgeSync(hWnd), null);
        RememberForegroundWindow();
        RefreshMenuState();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = true,
            Font = new Font("Segoe UI", 9F)
        };
        UiTheme.StyleMenu(menu);
        menu.HandleCreated += (_, _) => RefreshPinMenuImage(menu);
        menu.DpiChangedAfterParent += (_, _) => RefreshPinMenuImage(menu);
        menu.Opening += (_, _) => RefreshPinMenuImage(menu);
        menu.Opening += (_, _) => RefreshMenuState();

        menu.Items.Add(_countItem);
        UiTheme.StyleMenuItem(_countItem);
        menu.Items.Add(new ToolStripSeparator());

        _captureItem.Click += async (_, _) => await CaptureAndPinAsync();
        UiTheme.StyleMenuItem(_captureItem, emphasized: true);
        menu.Items.Add(_captureItem);

        var selectWindow = new ToolStripMenuItem("选择窗口置顶") { Image = _pinMenuImage, ToolTipText = "点击要置顶的应用窗口；右键或 Esc 取消" };
        selectWindow.Click += (_, _) => BeginWindowSelection();
        UiTheme.StyleMenuItem(selectWindow, emphasized: true);
        menu.Items.Add(selectWindow);
        menu.Items.Add(new ToolStripSeparator());
        UiTheme.StyleMenuItem(_screenshotsMenu);
        menu.Items.Add(_screenshotsMenu);

        menu.Items.Add(_managedWindowsMenu);
        UiTheme.StyleMenuItem(_managedWindowsMenu);
        UiTheme.StyleMenu((ToolStripDropDownMenu)_managedWindowsMenu.DropDown);
        _managedWindowsMenu.DropDownDirection = ToolStripDropDownDirection.Right;
        _managedWindowsMenu.DropDownOpening += (_, _) =>
        {
            Rectangle workArea = Screen.FromRectangle(menu.Bounds).WorkingArea;
            _managedWindowsMenu.DropDown.MaximumSize = new Size(Math.Min(UiTheme.Scale(440, menu.DeviceDpi), workArea.Width), workArea.Height);
        };
        // WinForms positions a ToolStripMenuItem drop-down using the menu's
        // screen coordinates, but DropDownOpening fires before the native menu
        // window has finished laying itself out.  Align on DropDownOpened and
        // convert the item's right edge from the owner menu's client space to
        // screen space; otherwise the child menu can retain the default gap.
        _managedWindowsMenu.DropDownOpened += (_, _) =>
        {
            AlignSubmenu(_managedWindowsMenu);
            // WinForms may apply its native menu placement once more after the
            // DropDownOpened event. Queue a second pass so the final window
            // position remains flush with the owner menu's visible edge.
            _managedWindowsMenu.DropDown.BeginInvoke((MethodInvoker)(() => AlignSubmenu(_managedWindowsMenu)));
        };

        _unpinAllWindowsMenu.Click += (_, _) => UnpinAllWindows();
        UiTheme.StyleMenuItem(_unpinAllWindowsMenu);
        menu.Items.Add(_unpinAllWindowsMenu);

        menu.Items.Add(new ToolStripSeparator());
        UiTheme.StyleMenuItem(_feedbackItem);
        _feedbackItem.ForeColor = UiTheme.Error;
        _feedbackItem.AccessibleName = "操作失败详情";
        _feedbackItem.Click += (_, _) => _feedbackAction?.Invoke();
        menu.Items.Add(_feedbackItem);

        var shortcutItem = new ToolStripMenuItem("快捷键设置");
        shortcutItem.Click += (_, _) => ShowShortcutSettings();
        UiTheme.StyleMenuItem(shortcutItem);
        menu.Items.Add(shortcutItem);

        _startupItem.Click += (_, _) => ToggleStartup();
        UiTheme.StyleMenuItem(_startupItem);
        menu.Items.Add(_startupItem);

        menu.Items.Add(new ToolStripSeparator());
        var aboutItem = new ToolStripMenuItem("关于");
        aboutItem.Click += (_, _) => ShowAbout();
        UiTheme.StyleMenuItem(aboutItem);
        menu.Items.Add(aboutItem);

        var exitItem = new ToolStripMenuItem("退出") { ToolTipText = "取消窗口置顶并关闭全部贴图；请先保存需要的图片" };
        exitItem.Click += (_, _) => ExitThread();
        UiTheme.StyleMenuItem(exitItem);
        menu.Items.Add(exitItem);
        return menu;
    }

    private static Bitmap CreatePinMenuImage(int dpi)
    {
        // Keep the shared image column and row height while reducing only the
        // visible pin. Both frames use final device pixels without resampling.
        int size = UiTheme.Scale(UiTheme.IconSize, dpi);
        using Bitmap pin = TrayIconFactory.CreateMenuPinImage(UiTheme.Scale(PinMenuIconSize, dpi));
        var image = new Bitmap(size, size);
        using Graphics graphics = Graphics.FromImage(image);
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(pin, (size - pin.Width) / 2, (size - pin.Height) / 2);
        return image;
    }

    private void RefreshPinMenuImage(ContextMenuStrip menu)
    {
        int size = UiTheme.Scale(UiTheme.IconSize, menu.DeviceDpi);
        if (_pinMenuImage.Width == size) return;
        Image previous = _pinMenuImage;
        _pinMenuImage = CreatePinMenuImage(menu.DeviceDpi);
        foreach (ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>()
            .Concat(_managedWindowsMenu.DropDownItems.OfType<ToolStripMenuItem>()))
            if (ReferenceEquals(item.Image, previous)) item.Image = _pinMenuImage;
        previous.Dispose();
    }

    private void RegisterHotKeys(string? loadWarning)
    {
        int toggleError = 0;
        if (_toggleHotKeyRegistered || (!_config.Shortcut.ConflictsWithCancel &&
            _hotKeyWindow.TryRegister(ToggleHotKeyId, _config.Shortcut, out toggleError)))
        {
            _toggleHotKeyRegistered = true;
        }
        else
        {
            int error = toggleError == 0 ? 1409 : toggleError;
            ShowHotKeyRegistrationError(_config.Shortcut, error);
        }

        if (_cancelHotKeyRegistered || _hotKeyWindow.TryRegister(CancelHotKeyId, new HotKeyBinding
            {
                Modifiers = NativeMethods.HotKeyModifiers.Control | NativeMethods.HotKeyModifiers.Shift,
                Key = (uint)Keys.Space
            }, out _))
        {
            _cancelHotKeyRegistered = true;
        }

        int captureError = 0;
        if (_screenshotHotKeyRegistered || _hotKeyWindow.TryRegister(ScreenshotHotKeyId, _config.ScreenshotShortcut, out captureError))
        {
            _screenshotHotKeyRegistered = true;
        }
        else
        {
            ShowMenuError(MenuErrorKind.ScreenshotShortcut,
                $"截图贴图快捷键 {_config.ScreenshotShortcut} 不可用（Windows 错误 {captureError}）。点击打开快捷键设置。",
                ShowShortcutSettings);
        }

        if (!string.IsNullOrWhiteSpace(loadWarning))
        {
            MessageBox.Show(loadWarning, "窗口置顶", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnHotKeyPressed(object? sender, int id)
    {
        if (_capturePending || _isExiting || _editingShortcuts) return;
        if (id == ToggleHotKeyId)
        {
            ToggleLastExternalWindow(useForeground: true);
        }
        else if (id == CancelHotKeyId)
        {
            UnpinLastExternalWindow(useForeground: true);
        }
        else if (id == ScreenshotHotKeyId)
        {
            _ = CaptureAndPinAsync();
        }
    }

    private void RememberForegroundWindow()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (_windowManager.IsEligibleWindow(foreground, _hotKeyWindow.WindowHandle))
        {
            _lastExternalWindow = foreground;
        }
    }

    private void ToggleLastExternalWindow(bool useForeground = false)
    {
        IntPtr hWnd = useForeground ? NativeMethods.GetForegroundWindow() : _lastExternalWindow;
        if (!_windowManager.TryToggle(hWnd, _hotKeyWindow.WindowHandle, out bool pinned, out string error))
        {
            ShowMenuError(MenuErrorKind.WindowPin, error);
            return;
        }

        _config.LastAction = pinned ? "Pin" : "Unpin";
        SaveConfigSilently();
        Notify(pinned ? "窗口已置顶" : "窗口已取消置顶", ToolTipIcon.Info);
        ClearMenuError();
        RefreshMenuState();
    }

    private void PinLastExternalWindow()
    {
        if (!_windowManager.TrySet(_lastExternalWindow, true, _hotKeyWindow.WindowHandle, out string error))
        {
            ShowMenuError(MenuErrorKind.WindowPin, error);
            return;
        }

        _config.LastAction = "Pin";
        SaveConfigSilently();
        Notify("窗口已置顶", ToolTipIcon.Info);
        ClearMenuError();
        RefreshMenuState();
    }

    private void UnpinLastExternalWindow(bool useForeground = false)
    {
        IntPtr hWnd = useForeground ? NativeMethods.GetForegroundWindow() : _lastExternalWindow;
        if (!_windowManager.TrySet(hWnd, false, _hotKeyWindow.WindowHandle, out string error))
        {
            ShowMenuError(MenuErrorKind.WindowUnpin, error);
            return;
        }

        _config.LastAction = "Unpin";
        SaveConfigSilently();
        Notify("窗口已取消置顶", ToolTipIcon.Info);
        ClearMenuError();
        RefreshMenuState();
    }

    private void BeginWindowSelection()
    {
        if (_capturePending) return;
        _menu.Close();
        _selectionOverlay.BeginSelection();
    }

    private async Task CaptureAndPinAsync()
    {
        if (_capturePending || _isExiting) return;
        _capturePending = true;
        try
        {
            _menu.Close();
            _selectionOverlay.Hide();
            // Let the tray menu disappear before freezing the desktop.
            await Task.Delay(180);
            if (_isExiting) return;

            Rectangle screenBounds = SystemInformation.VirtualScreen;
            using Bitmap desktop = ScreenCapture.Capture(screenBounds);
            using var overlay = new ScreenshotSelectionOverlay(desktop, screenBounds);
            _screenshotOverlay = overlay;
            if (overlay.ShowDialog() != DialogResult.OK || _isExiting) return;

            Rectangle selection = overlay.SelectedRegion;
            AddPinnedImage(ScreenCapture.Crop(desktop, selection),
                new Point(screenBounds.Left + selection.Left, screenBounds.Top + selection.Top));
            ClearMenuError();
        }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or
            System.Runtime.InteropServices.ExternalException or ArgumentException or InvalidOperationException or OutOfMemoryException)
        {
            if (!_isExiting)
            {
                ShowMenuError(MenuErrorKind.Screenshot, $"无法生成截图贴图：{ex.Message}");
            }
        }
        finally
        {
            _screenshotOverlay = null;
            _capturePending = false;
        }
    }

    private void AddPinnedImage(Bitmap image, Point location)
    {
        PinnedImageWindow? window = null;
        try
        {
            window = new PinnedImageWindow(image, location, ++_screenshotNumber);
            _screenshots.Add(window);
            window.FormClosed += (_, _) =>
            {
                _screenshots.Remove(window);
                if (!_isExiting) RefreshMenuState();
            };
            window.Show();
            RefreshMenuState();
        }
        catch
        {
            if (window is not null)
            {
                _screenshots.Remove(window);
                window.Dispose();
            }
            else
            {
                image.Dispose();
            }
            throw;
        }
    }

    private void OnWindowSelected(object? sender, IntPtr hWnd)
    {
        if (!_windowManager.TrySet(hWnd, true, _hotKeyWindow.WindowHandle, out string error))
        {
            ShowMenuError(MenuErrorKind.WindowPin, error);
            return;
        }

        _lastExternalWindow = hWnd;
        _config.LastAction = "SelectAndPin";
        SaveConfigSilently();
        Notify("所选窗口已置顶", ToolTipIcon.Info);
        ClearMenuError();
        RefreshMenuState();
    }

    private void RefreshMenuState()
    {
        _countItem.Text = $"置顶窗口 {_windowManager.Count} · 截图贴图 {_screenshots.Count}";
        _countItem.ToolTipText = "当前由本工具管理的窗口和贴图数量";
        _startupItem.Image = _config.StartWithWindows ? _pinMenuImage : null;
        _startupItem.ToolTipText = _config.StartWithWindows ? "已开启，点击关闭开机启动" : "已关闭，点击开启开机启动";
        SyncPinnedBadges();
        _screenshotsMenu.Enabled = _screenshots.Count > 0;
        _unpinAllWindowsMenu.Enabled = _windowManager.Count > 0;
        _screenshotsMenu.ToolTipText = _screenshots.Count == 0 ? "暂无截图贴图" : $"关闭全部 {_screenshots.Count} 张截图贴图；请先保存需要的图片";
        _unpinAllWindowsMenu.ToolTipText = _windowManager.Count == 0 ? "暂无由本工具置顶的窗口" : $"取消本工具设置的 {_windowManager.Count} 个窗口置顶";
        // This command closes all pinned screenshots at once.  Keep it as a
        // plain menu item: individual screenshot entries are intentionally not
        // exposed here.
        _screenshotsMenu.DropDownItems.Clear();
        ClearMenuItems(_managedWindowsMenu);
        IReadOnlyList<ManagedWindow> windows = _windowManager.GetManagedWindows();
        _managedWindowsMenu.Enabled = windows.Count > 0;
        if (windows.Count == 0)
        {
            var emptyItem = new ToolStripMenuItem("暂无置顶窗口") { Enabled = false };
            UiTheme.StyleMenuItem(emptyItem);
            _managedWindowsMenu.DropDownItems.Add(emptyItem);
            return;
        }

        foreach (ManagedWindow window in windows)
        {
            string label = window.Title.Replace("&", "&&");
            int maxWidth = UiTheme.Scale(360, _menu.DeviceDpi);
            while (label.Length > 1 && TextRenderer.MeasureText(label, UiTheme.MenuFont).Width > maxWidth)
                label = label[..^1];
            if (label != window.Title.Replace("&", "&&")) label += "…";
            var item = new ToolStripMenuItem(label)
            {
                Tag = window.Handle,
                Image = _pinMenuImage,
                ToolTipText = $"已置顶：{window.Title}"
            };
            UiTheme.StyleMenuItem(item);
            item.Click += (_, _) =>
            {
                if (!_windowManager.TryUnpinManaged((IntPtr)item.Tag!, out string error))
                {
                    ShowMenuError(MenuErrorKind.WindowUnpin, error);
                    return;
                }

                _config.LastAction = "UnpinSpecific";
                SaveConfigSilently();
                Notify("窗口已取消置顶", ToolTipIcon.Info);
                ClearMenuError();
                RefreshMenuState();
            };
            _managedWindowsMenu.DropDownItems.Add(item);
        }
    }

    private static void ClearMenuItems(ToolStripMenuItem menu)
    {
        while (menu.DropDownItems.Count > 0)
        {
            ToolStripItem item = menu.DropDownItems[0];
            menu.DropDownItems.RemoveAt(0);
            item.Dispose();
        }
    }

    private static void AlignSubmenu(ToolStripMenuItem item)
    {
        if (!item.DropDown.Visible || item.GetCurrentParent() is not ToolStrip parent)
        {
            return;
        }

        // Align to the rendered owner menu's outer edge.  The item bounds can
        // include logical image/arrow columns beyond the native popup width;
        // using the owner's screen bounds targets the edge users actually see.
        Point ownerTopLeft = parent.PointToScreen(new Point(0, item.Bounds.Top));
        // Center the popup's visible content on the owner row.  WinForms
        // otherwise aligns the popup's outer top edge, while its own padding
        // shifts a one-row submenu visibly downward.
        Rectangle owner = new(parent.PointToScreen(Point.Empty), parent.Size);
        Rectangle row = new(ownerTopLeft, item.Size);
        Rectangle target = UiTheme.PlaceSubmenu(owner, row, item.DropDown.Size, Screen.FromRectangle(owner).WorkingArea);
        Point location = item.DropDown.Location;
        if (location != target.Location)
        {
            item.DropDown.Location = target.Location;
        }
        // The native menu manager can restore its default 16px separation after
        // SetBounds. Move the already visible popup at the Win32 level as the
        // final placement pass so its painted border meets the owner edge.
        if (item.DropDown.IsHandleCreated)
        {
            NativeMethods.SetWindowPos(item.DropDown.Handle, IntPtr.Zero,
                target.X, target.Y, 0, 0,
                NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
        }
    }

    private void UnpinAllWindows()
    {
        int count = _windowManager.UnpinAllManaged(out string? error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            ShowMenuError(MenuErrorKind.WindowUnpin, error);
        }
        else
        {
            Notify(count == 0 ? "暂无由本工具置顶的窗口" : $"已取消 {count} 个窗口置顶", ToolTipIcon.Info);
            ClearMenuError();
        }

        _config.LastAction = "UnpinAll";
        SaveConfigSilently();
        RefreshMenuState();
    }

    private void CloseAllScreenshots()
    {
        int count = _screenshots.Count;
        foreach (PinnedImageWindow image in _screenshots.ToArray())
        {
            image.Close();
        }

        Notify(count == 0 ? "暂无截图贴图" : $"已关闭 {count} 张截图贴图", ToolTipIcon.Info);
        if (count > 0) ClearMenuError();
        RefreshMenuState();
    }

    private void ShowShortcutSettings()
    {
        if (_editingShortcuts) return;
        _editingShortcuts = true;
        UnregisterActionHotKeys();
        if (_cancelHotKeyRegistered) _hotKeyWindow.Unregister(CancelHotKeyId);
        _cancelHotKeyRegistered = false;
        try
        {
            using var dialog = new HotKeySettingsForm(_config.Shortcut, _config.ScreenshotShortcut, ApplyShortcuts);
            dialog.ShowDialog();
        }
        finally
        {
            _editingShortcuts = false;
            RegisterHotKeys(null);
        }
    }

    internal string? ApplyShortcuts(HotKeyBinding window, HotKeyBinding screenshot)
    {
        string? validation = HotKeySettingsForm.ValidateBindings(window, screenshot);
        if (validation is not null) return validation;

        bool restoreToggle = _toggleHotKeyRegistered;
        bool restoreScreenshot = _screenshotHotKeyRegistered;
        UnregisterActionHotKeys();
        bool applied = false;
        try
        {
            _toggleHotKeyRegistered = _hotKeyWindow.TryRegister(ToggleHotKeyId, window, out int windowError);
            if (!_toggleHotKeyRegistered)
                return $"无法注册窗口置顶快捷键 {window}（错误 {windowError}），请更换组合。";
            _screenshotHotKeyRegistered = _hotKeyWindow.TryRegister(ScreenshotHotKeyId, screenshot, out int screenshotError);
            if (!_screenshotHotKeyRegistered)
                return $"无法注册截图贴图快捷键 {screenshot}（错误 {screenshotError}），请更换组合。";

            HotKeyBinding oldWindow = _config.Shortcut, oldScreenshot = _config.ScreenshotShortcut;
            _config.Shortcut = window;
            _config.ScreenshotShortcut = screenshot;
            if (!_configStore.TrySave(_config, out string? saveError))
            {
                _config.Shortcut = oldWindow;
                _config.ScreenshotShortcut = oldScreenshot;
                return $"无法保存快捷键设置：{saveError}";
            }
            applied = true;
            ClearMenuError();
            RefreshMenuState();
            return null;
        }
        finally
        {
            if (!applied)
            {
                UnregisterActionHotKeys();
                _toggleHotKeyRegistered = restoreToggle &&
                    _hotKeyWindow.TryRegister(ToggleHotKeyId, _config.Shortcut, out _);
                _screenshotHotKeyRegistered = restoreScreenshot &&
                    _hotKeyWindow.TryRegister(ScreenshotHotKeyId, _config.ScreenshotShortcut, out _);
            }
        }
    }

    private void UnregisterActionHotKeys()
    {
        if (_toggleHotKeyRegistered) _hotKeyWindow.Unregister(ToggleHotKeyId);
        if (_screenshotHotKeyRegistered) _hotKeyWindow.Unregister(ScreenshotHotKeyId);
        _toggleHotKeyRegistered = false;
        _screenshotHotKeyRegistered = false;
    }

    private void ToggleStartup()
    {
        bool requested = !_config.StartWithWindows;
        if (!StartupService.TrySetEnabled(requested, out string? error))
        {
            ShowMenuError(MenuErrorKind.Startup, $"无法修改开机启动设置：{error}。点击重试。", ToggleStartup);
            return;
        }

        _startupItem.Image = requested ? _pinMenuImage : null;
        _config.StartWithWindows = requested;
        SaveConfigSilently();
        ClearMenuError();
        RefreshMenuState();
    }

    private void ShowAbout()
    {
        if (_isExiting) return;
        if (_aboutForm is { IsDisposed: false } existing)
        {
            existing.Activate();
            return;
        }

        var about = new AboutForm();
        _aboutForm = about;
        about.FormClosed += (_, _) => _aboutForm = null;
        // A modal dialog disables existing screenshot windows on this thread.
        about.Show();
    }

    private void ShowHotKeyRegistrationError(HotKeyBinding binding, int errorCode)
    {
        ShowMenuError(MenuErrorKind.WindowShortcut,
            $"窗口置顶快捷键 {binding} 不可用（Windows 错误 {errorCode}）。点击打开快捷键设置。",
            ShowShortcutSettings);
    }

    private void Notify(string message, ToolTipIcon icon)
    {
        // Status changes are reflected directly in the tray menu.  Keep this
        // method as a single call site for compatibility, but do not display
        // balloon notifications.
    }

    internal static string GetMenuErrorTitle(MenuErrorKind kind) => kind switch
    {
        MenuErrorKind.WindowShortcut => "窗口置顶快捷键不可用",
        MenuErrorKind.ScreenshotShortcut => "截图贴图快捷键不可用",
        MenuErrorKind.WindowPin => "窗口置顶失败",
        MenuErrorKind.WindowUnpin => "取消窗口置顶失败",
        MenuErrorKind.Screenshot => "截图贴图失败",
        MenuErrorKind.Startup => "开机启动设置失败",
        _ => "操作失败"
    };

    private void ShowMenuError(MenuErrorKind kind, string detail, Action? action = null)
    {
        _feedbackAction = action;
        _feedbackItem.Text = GetMenuErrorTitle(kind);
        _feedbackItem.ToolTipText = detail;
        _feedbackItem.AccessibleName = $"{_feedbackItem.Text}：{detail}";
        _feedbackItem.Enabled = true;
        _feedbackItem.Visible = true;
    }

    private void ClearMenuError()
    {
        _feedbackAction = null;
        _feedbackItem.Visible = false;
        _feedbackItem.Enabled = false;
        _feedbackItem.Text = string.Empty;
        _feedbackItem.ToolTipText = string.Empty;
        _feedbackItem.AccessibleName = "操作失败详情";
    }

    private void SaveConfigSilently()
    {
        _configStore.TrySave(_config, out _);
    }

    private void SyncPinnedBadges()
    {
        IReadOnlyList<ManagedWindow> managedWindows = _windowManager.GetManagedWindows();
        HashSet<IntPtr> activeHandles = managedWindows.Select(window => window.Handle).ToHashSet();
        foreach (IntPtr handle in _badges.Keys.Where(handle => !activeHandles.Contains(handle)).ToArray())
        {
            _badges[handle].Dispose();
            _badges.Remove(handle);
        }

        foreach (ManagedWindow window in managedWindows)
        {
            if (!_badges.TryGetValue(window.Handle, out PinnedBadgeWindow? badge))
            {
                badge = new PinnedBadgeWindow(window.Handle);
                _badges[window.Handle] = badge;
            }

            badge.Sync();
        }
    }

    private void QueueBadgeSync(IntPtr hWnd)
    {
        if (!_isExiting && _badges.TryGetValue(hWnd, out PinnedBadgeWindow? badge))
        {
            badge.QueueSync();
        }
    }

    protected override void ExitThreadCore()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _aboutForm?.Close();
        _screenshotOverlay?.Close();
        foreach (PinnedImageWindow image in _screenshots.ToArray()) image.Close();
        _screenshots.Clear();
        _selectionOverlay.Hide();
        _foregroundTimer.Stop();
        _foregroundTimer.Dispose();
        _locationMonitor.Dispose();
        _windowManager.RestoreManagedWindows();
        foreach (PinnedBadgeWindow badge in _badges.Values)
        {
            badge.Dispose();
        }
        _badges.Clear();
        if (_toggleHotKeyRegistered) _hotKeyWindow.Unregister(ToggleHotKeyId);
        if (_cancelHotKeyRegistered) _hotKeyWindow.Unregister(CancelHotKeyId);
        if (_screenshotHotKeyRegistered) _hotKeyWindow.Unregister(ScreenshotHotKeyId);
        _hotKeyWindow.Dispose();
        _selectionOverlay.Dispose();
        _menu.Dispose();
        _pinMenuImage.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
        base.ExitThreadCore();
    }
}
