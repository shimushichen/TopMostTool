using System.Drawing.Imaging;
using TopMostTool;

internal static partial class Program
{
    private static async Task TestMenusAndShortcuts()
    {
        TestShortcutConfig();
        TestShortcutEditor();
        using var image = new PinnedImageWindow(CreatePattern(), new Point(150, 150), 1);
        ContextMenuStrip imageMenu = image.ContextMenuStrip!;
        using (var textPreview = new Bitmap(300, 48))
        using (Graphics graphics = Graphics.FromImage(textPreview))
        {
            graphics.Clear(Color.White);
            ToolStripMenuItem item = imageMenu.Items.OfType<ToolStripMenuItem>().First();
            imageMenu.Renderer.DrawItemText(new ToolStripItemTextRenderEventArgs(graphics, item,
                "Ctrl + +", new Rectangle(12, 0, 276, 40), Color.Black, item.Font,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter));
            int minX = textPreview.Width, maxX = -1;
            for (int y = 0; y < textPreview.Height; y++)
                for (int x = 0; x < textPreview.Width; x++)
                    if (textPreview.GetPixel(x, y).R < 150)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                    }
            Check(minX > 180 && maxX > 260, "shortcut glyphs render in the right column rather than over the command");
        }
        imageMenu.Show(100, 100);
        try
        {
            await Task.Delay(80);
            using Bitmap screenPreview = ScreenCapture.Capture(imageMenu.Bounds);
            screenPreview.Save(Path.Combine(Artifacts, "screenshot-menu-screen.png"));
            SaveControlPreview(imageMenu, "screenshot-menu.png");
            ToolStripMenuItem zoomIn = imageMenu.Items.OfType<ToolStripMenuItem>().First();
            Check(!zoomIn.ShowShortcutKeys &&
                imageMenu.Items.OfType<ToolStripMenuItem>().All(item => !item.ShowShortcutKeys &&
                    string.IsNullOrEmpty(item.ShortcutKeyDisplayString)),
                "screenshot menu hides shortcut labels while retaining bindings");
            Check(zoomIn.ShortcutKeys == (Keys.Control | Keys.Oemplus), "readable zoom shortcut preserves the real key binding");
            Check(imageMenu.ShowImageMargin && imageMenu.Items.OfType<ToolStripMenuItem>().All(item => item.Image is not null),
                "screenshot context menu uses the same icon column as the tray menu");
            double previousZoom = image.Zoom;
            zoomIn.PerformClick();
            Check(image.Zoom > previousZoom, "screenshot zoom command still performs its action");
        }
        finally { imageMenu.Close(); }

        HotKeyBinding window = TestBinding(Keys.F7), screenshot = TestBinding(Keys.F8);
        var store = new ConfigStore(Path.Combine(Artifacts, "menu-shortcuts-config.json"));
        Check(store.TrySave(new AppConfig
        {
            Shortcut = window, ScreenshotShortcut = screenshot, ShowNotifications = false
        }, out _), "isolated shortcut configuration saves");
        using var context = new TrayApplicationContext(store);
        try
        {
            Check(Field<bool>(context, "_screenshotHotKeyRegistered"), "configured screenshot shortcut registers globally");
            AppConfig config = Field<AppConfig>(context, "_config");
            ContextMenuStrip menu = Field<ContextMenuStrip>(context, "_menu");
            ToolStripMenuItem startup = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "开机启动");
            bool startupState = config.StartWithWindows;
            config.StartWithWindows = true;
            Invoke(context, "RefreshMenuState");
            Check(startup.Image is not null && !startup.Checked && !startup.CheckOnClick,
                "enabled startup uses the tray icon without a checked background");
            menu.Show(100, 100);
            await Task.Delay(80);
            SaveControlPreview(menu, "startup-enabled.png");
            menu.Close();
            config.StartWithWindows = false;
            Invoke(context, "RefreshMenuState");
            Check(startup.Image is null && !startup.Checked, "disabled startup hides the icon without changing the registry");
            config.StartWithWindows = startupState;
            Invoke(context, "RefreshMenuState");

            Check(context.ApplyShortcuts(window, window) is not null &&
                config.Shortcut == window && config.ScreenshotShortcut == screenshot,
                "duplicate shortcuts are rejected without replacing current settings");
            HotKeyBinding unavailable = TestBinding(Keys.F10);
            using (var blocker = new HotKeyMessageWindow())
            {
                Check(blocker.TryRegister(95, unavailable, out _), "test reserves a different shortcut for conflict checking");
                try
                {
                    Check(context.ApplyShortcuts(TestBinding(Keys.F9), unavailable) is not null,
                        "occupied screenshot shortcut is reported to the settings form");
                    Check(config.Shortcut == window && config.ScreenshotShortcut == screenshot &&
                        Field<bool>(context, "_toggleHotKeyRegistered") && Field<bool>(context, "_screenshotHotKeyRegistered"),
                        "registration failure restores both original shortcuts");
                }
                finally { blocker.Unregister(95); }
            }
            Check(context.ApplyShortcuts(screenshot, window) is null, "window and screenshot shortcuts can be swapped atomically");
            AppConfig reloaded = store.Load(out _);
            Check(reloaded.Shortcut == screenshot && reloaded.ScreenshotShortcut == window,
                "both changed shortcuts survive configuration reload");
            ToolStripMenuItem captureItem = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "截图贴图");
            Check(!captureItem.ShowShortcutKeys && string.IsNullOrEmpty(captureItem.ShortcutKeyDisplayString),
                "tray screenshot command hides the global shortcut after settings change");

            byte[] savedConfig = File.ReadAllBytes(store.ConfigPath);
            File.SetAttributes(store.ConfigPath, FileAttributes.ReadOnly);
            try
            {
                Check(context.ApplyShortcuts(TestBinding(Keys.F9), TestBinding(Keys.F10)) is not null &&
                    config.Shortcut == screenshot && config.ScreenshotShortcut == window &&
                    Field<bool>(context, "_toggleHotKeyRegistered") && Field<bool>(context, "_screenshotHotKeyRegistered") &&
                    File.ReadAllBytes(store.ConfigPath).SequenceEqual(savedConfig),
                    "configuration write failure preserves the stored settings and active shortcuts");
            }
            finally { File.SetAttributes(store.ConfigPath, FileAttributes.Normal); }

            HotKeyMessageWindow receiver = Field<HotKeyMessageWindow>(context, "_hotKeyWindow");
            // Deliver through the native receiver; the full regression suite
            // separately exercises the physical desktop shortcut path.
            NativeMethods.SendMessage(receiver.WindowHandle, NativeMethods.WmHotKey, (IntPtr)3, IntPtr.Zero);
            await Until(() => Application.OpenForms.OfType<ScreenshotSelectionOverlay>().Any(),
                "screenshot hotkey dispatch opens the region selector");
            PostMessage(receiver.WindowHandle, NativeMethods.WmHotKey, (IntPtr)3, IntPtr.Zero);
            await Task.Delay(30);
            Check(Application.OpenForms.OfType<ScreenshotSelectionOverlay>().Count() == 1,
                "repeated screenshot shortcut does not create a second selector");
            ScreenshotSelectionOverlay overlay = Application.OpenForms.OfType<ScreenshotSelectionOverlay>().Single();
            Mouse(overlay, 0x0201, 120, 120);
            Mouse(overlay, 0x0200, 240, 200);
            Mouse(overlay, 0x0202, 240, 200);
            await Until(() => !Field<bool>(context, "_capturePending"), "hotkey screenshot capture finishes");
            var images = Field<List<PinnedImageWindow>>(context, "_screenshots");
            Check(images.Count == 1 && images[0].ImageSize == new Size(120, 80),
                "hotkey capture creates the selected pinned screenshot");
            ToolStripMenuItem closeAll = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "关闭全部截图贴图");
            Check(!closeAll.HasDropDownItems && closeAll.Enabled, "screenshots keep one close-all command without individual entries");
            closeAll.PerformClick();
            Check(images.Count == 0, "close-all command closes a screenshot captured by hotkey");
        }
        finally { Invoke(context, "ExitThreadCore"); }
        using var afterExit = new HotKeyMessageWindow();
        Check(afterExit.TryRegister(96, window, out _), "application exit releases the configured screenshot shortcut");
        afterExit.Unregister(96);
    }

    private static HotKeyBinding TestBinding(Keys key) => new()
    {
        Key = (uint)key,
        Modifiers = NativeMethods.HotKeyModifiers.Control | NativeMethods.HotKeyModifiers.Alt | NativeMethods.HotKeyModifiers.Shift
    };

    private static void TestShortcutConfig()
    {
        var store = new ConfigStore(Path.Combine(Artifacts, "legacy-shortcuts.json"));
        File.WriteAllText(store.ConfigPath, "{}");
        AppConfig migrated = store.Load(out _);
        Check(migrated.Shortcut == HotKeyBinding.Default && migrated.ScreenshotShortcut == HotKeyBinding.ScreenshotDefault,
            "existing configurations gain a default screenshot shortcut");
        store.TrySave(new AppConfig { Shortcut = HotKeyBinding.ScreenshotDefault }, out _);
        migrated = store.Load(out _);
        Check(migrated.Shortcut == HotKeyBinding.ScreenshotDefault && migrated.ScreenshotShortcut != migrated.Shortcut,
            "migration preserves an existing window shortcut that uses the new screenshot default");
        Check(HotKeySettingsForm.ValidateBindings(HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault) is null &&
            HotKeySettingsForm.ValidateBindings(HotKeyBinding.Default, HotKeyBinding.Default) is not null &&
            HotKeySettingsForm.ValidateBindings(HotKeyBinding.Default, new HotKeyBinding
            {
                Key = (uint)Keys.Space, Modifiers = NativeMethods.HotKeyModifiers.Control | NativeMethods.HotKeyModifiers.Shift
            }) is not null, "settings reject duplicate and reserved cancel shortcuts");
        Check(!new HotKeyBinding { Key = (uint)Keys.A }.IsValid, "an unmodified key cannot become a global shortcut");
    }

    private static void TestShortcutEditor()
    {
        using var form = new HotKeySettingsForm(HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-10000, -10000);
        form.Show();
        Application.DoEvents();
        form.PerformLayout();
        SaveControlPreview(form, "shortcut-settings.png");
        HotKeyEditor[] editors = FindControls<HotKeyEditor>(form).ToArray();
        Check(editors.Length == 2 && editors.All(editor => form.ClientRectangle.Contains(editor.Bounds)),
            "settings contain separate window and screenshot editors");
        Check(editors.All(editor => !FindControls<TextBox>(editor).Single().Focused),
            "settings open without arming either shortcut recorder");
        Check(editors.All(editor => FindControls<TextBox>(editor).Single().SelectionLength == 0),
            "settings do not select an existing shortcut on open");
        Check(editors.All(editor => FindControls<Label>(editor).Any(label => label.Text.StartsWith("点击输入框录入"))),
            "settings show the idle shortcut hint until the user activates a field");
        HotKeyEditor screenshot = editors.Single(editor => FindControls<TextBox>(editor).Single().AccessibleName == "截图贴图快捷键");
        TextBox screenshotBox = FindControls<TextBox>(screenshot).Single();
        // Explicit activation is covered by the editor's Enter path; keep the
        // host focus on the hidden test window before the native hotkey check.
        Invoke(screenshot, "CaptureKey", null!, new KeyEventArgs(Keys.Control | Keys.Alt | Keys.K));
        Check(form.Binding == HotKeyBinding.Default && form.ScreenshotBinding == new HotKeyBinding
        {
            Key = (uint)Keys.K, Modifiers = NativeMethods.HotKeyModifiers.Control | NativeMethods.HotKeyModifiers.Alt
        }, "editing the screenshot shortcut leaves the window shortcut intact");
        Check(FindControls<TextBox>(screenshot).Single().Text == "Ctrl + Alt + K", "screenshot editor displays the captured combination");
        Check(form.DialogResult == DialogResult.None, "editing shortcut fields alone does not save settings");
        Check(TrayApplicationContext.GetMenuErrorTitle(MenuErrorKind.ScreenshotShortcut) == "截图贴图快捷键不可用" &&
            TrayApplicationContext.GetMenuErrorTitle(MenuErrorKind.Startup) == "开机启动设置失败" &&
            TrayApplicationContext.GetMenuErrorTitle(MenuErrorKind.WindowPin) == "窗口置顶失败" &&
            TrayApplicationContext.GetMenuErrorTitle(MenuErrorKind.Screenshot) == "截图贴图失败",
            "tray error state maps each failure source to a concrete visible title");
    }

    private static void SaveControlPreview(Control control, string name)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        bitmap.Save(Path.Combine(Artifacts, name), ImageFormat.Png);
    }
}
