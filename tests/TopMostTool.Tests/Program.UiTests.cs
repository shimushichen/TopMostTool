using TopMostTool;

internal static partial class Program
{
    private static async Task TestUiSystem()
    {
        TestPopupGeometry();
        await TestSettingsFeedback();
        await TestShortcutCompactLayout();
        await TestDialogTypography();
        await TestPopupEdges();
        await TestTrayPopover();
        await TestImageKeyboard();
        using var selection = new WindowSelectionOverlay();
        int cancelled = 0;
        selection.SelectionCancelled += (_, _) => cancelled++;
        selection.BeginSelection();
        await Task.Delay(80);
        SaveControlPreview(selection, "window-selection.png");
        Mouse(selection, 0x0204, 200, 200);
        Check(!selection.Visible && cancelled == 1, "window selection supports right-click cancellation");
        selection.BeginSelection();
        Invoke(selection, "OnKeyDown", new KeyEventArgs(Keys.Escape));
        Check(!selection.Visible && cancelled == 2, "window selection supports Escape cancellation after reopening");
    }

    private static void TestPopupGeometry()
    {
        foreach (Rectangle area in new[] { new Rectangle(0, 0, 1920, 1040), new Rectangle(-1920, -300, 1920, 1080) })
        {
            var owner = new Rectangle(area.Right - 250, area.Bottom - 450, 240, 440);
            Rectangle row = new(owner.Left, owner.Top + 340, owner.Width, 40);
            Rectangle popup = UiTheme.PlaceSubmenu(owner, row, new Size(320, 700), area);
            Check(area.Contains(popup) && popup.Right == owner.Left,
                $"submenu opens left and stays within work area at {area.Location}");
            Rectangle huge = UiTheme.PlaceSubmenu(owner, row, new Size(3000, 3000), area);
            Check(area.Contains(huge), "oversized submenu placement fits the available work area");
        }
    }

    private static async Task TestSettingsFeedback()
    {
        using var form = new HotKeySettingsForm(HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault,
            (_, _) => "无法保存配置：测试存储不可写");
        form.Show();
        await Task.Delay(40);
        HotKeyEditor[] editors = FindControls<HotKeyEditor>(form).ToArray();
        Button save = FindControls<Button>(form).Single(button => button.AccessibleName == "保存快捷键");
        Label status = FindControls<Label>(form).Single(label => label.AccessibleName == "快捷键校验状态");
        editors[1].SetBinding(editors[0].Binding);
        Check(!save.Enabled && status.Text.Contains("相同"), "duplicate shortcut disables Save and explains the conflict inline");
        SaveControlPreview(form, "shortcut-conflict.png");
        editors[1].SetBinding(new HotKeyBinding { Key = (uint)Keys.Space, Modifiers = NativeMethods.HotKeyModifiers.Control | NativeMethods.HotKeyModifiers.Shift });
        Check(!save.Enabled && status.Text.Contains("已用于"), "reserved shortcut is explained before saving");
        FindControls<Button>(form).Single(button => button.AccessibleName == "恢复默认快捷键").PerformClick();
        Check(save.Enabled && form.Binding == HotKeyBinding.Default && form.ScreenshotBinding == HotKeyBinding.ScreenshotDefault,
            "restoring defaults clears the conflict without saving");
        save.PerformClick();
        Check(form.DialogResult == DialogResult.None && status.Text.Contains("原有快捷键继续生效"),
            "save failure keeps settings open and explains the original binding is retained");
        SaveControlPreview(form, "shortcut-save-error.png");
        CheckVisibleLayout(form);
        form.Close();
    }

    private static async Task TestShortcutCompactLayout()
    {
        foreach (float factor in new[] { 1F, 1.25F, 1.5F, 2F })
        {
            using var form = new HotKeySettingsForm(HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault);
            foreach (Control control in FindControls<Control>(form))
                control.Font = new Font(control.Font.FontFamily, control.Font.Size * factor, control.Font.Style);
            form.StartPosition = FormStartPosition.Manual;
            Rectangle area = Screen.PrimaryScreen!.WorkingArea;
            form.Location = new Point(area.Left + 40, area.Top + 40);
            form.Show();
            await Task.Delay(40);
            form.PerformLayout();
            HotKeyEditor[] editors = FindControls<HotKeyEditor>(form).ToArray();
            Check(editors.Length == 2, $"compact settings layout has two cards at {factor:0.##}x text");
            int gap = editors[1].Top - editors[0].Bottom;
            Check(gap <= UiTheme.Scale(16, form.DeviceDpi) + 2,
                $"shortcut cards keep a compact gap at {factor:0.##}x text ({gap}px)");
            Check(editors.All(editor => editor.Height <= UiTheme.Scale(190, form.DeviceDpi) * factor + 12),
                $"shortcut cards shrink to their content at {factor:0.##}x text");
            CheckShortcutAlignment(form, factor);
            CheckVisibleLayout(form);
            Size originalClient = form.ClientSize;
            Size originalWindow = form.Size;
            int originalContentBottom = form.ContentBottom;
            Size? settledScrollingClient = null;
            Point[] destinations =
            {
                new(area.Left + 28, area.Top + 28),
                new(Math.Max(area.Left, area.Right - form.Width - 36), area.Top + 52),
                new(area.Left + 64, Math.Max(area.Top, area.Bottom - form.Height - 32))
            };
            foreach (Point destination in destinations)
            {
                NativeMethods.SetWindowPos(form.Handle, IntPtr.Zero, destination.X, destination.Y, 0, 0,
                    NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
                await Task.Delay(30);
                Application.DoEvents();
                bool clientStable;
                if (form.AutoScroll)
                {
                    settledScrollingClient ??= form.ClientSize;
                    clientStable = form.ClientSize == settledScrollingClient &&
                        form.ClientSize.Height == originalClient.Height;
                }
                else clientStable = form.ClientSize == originalClient;
                Check(clientStable && form.Size == originalWindow && form.ContentBottom == originalContentBottom,
                    $"native move preserves settings layout at {factor:0.##}x text (client {originalClient} -> {form.ClientSize}, outer {originalWindow} -> {form.Size}, content {originalContentBottom} -> {form.ContentBottom}, scroll {form.AutoScroll})");
                Check(form.ContentBottom == form.ClientSize.Height || form.AutoScroll,
                    $"moved settings dialog has no unused footer at {factor:0.##}x text (client {form.ClientSize.Height}, content {form.ContentBottom})");
                Check(!form.HorizontalScroll.Visible,
                    $"settings reserves vertical scrollbar width without horizontal scrolling at {factor:0.##}x text");
                CheckShortcutAlignment(form, factor);
            }
            SaveControlPreview(form, factor == 1F ? "shortcut-settings-compact.png" : $"shortcut-settings-compact-{factor:0.##}.png");
            if (factor == 1F)
            {
                form.Activate();
                await Task.Delay(60);
                SaveNativeWindowPreview(form, "shortcut-settings-after-native-move.png");
            }
            form.Close();
        }

        using var errorForm = new HotKeySettingsForm(HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault,
            (_, _) => "无法保存配置：测试存储不可写，请检查配置目录权限后重试。\n原有快捷键继续生效。");
        errorForm.Show();
        await Task.Delay(30);
        FindControls<Button>(errorForm).Single(button => button.AccessibleName == "保存快捷键").PerformClick();
        errorForm.PerformLayout();
        Label error = FindControls<Label>(errorForm).Single(label => label.AccessibleName == "快捷键校验状态");
        Check(error.Height >= error.GetPreferredSize(new Size(error.Width, 0)).Height,
            "multi-line shortcut save errors reserve their full message height");
        int retained = error.Text.IndexOf("原有快捷键继续生效。", StringComparison.Ordinal);
        Check(retained >= 0 && retained == error.Text.LastIndexOf("原有快捷键继续生效。", StringComparison.Ordinal),
            "save failure shows the original-binding guidance only once");
        Check(errorForm.ContentBottom == errorForm.ClientSize.Height || errorForm.AutoScroll,
            "expanded validation text refits the dialog without an unused footer");
        SaveControlPreview(errorForm, "shortcut-settings-compact-error.png");
        CheckVisibleLayout(errorForm);
        errorForm.Close();
    }

    private static void CheckShortcutAlignment(HotKeySettingsForm form, float factor)
    {
        HotKeyEditor[] editors = FindControls<HotKeyEditor>(form).ToArray();
        Check(editors[0].Left == editors[1].Left && editors[0].Width == editors[1].Width,
            $"shortcut cards share one left edge and width at {factor:0.##}x text");
        foreach (HotKeyEditor editor in editors)
        {
            CheckBox[] modifiers = FindControls<CheckBox>(editor).ToArray();
            Check(modifiers.All(modifier => modifier is ModifierKeyCap) &&
                modifiers.All(modifier => modifier.Top == modifiers[0].Top && modifier.Height == modifiers[0].Height),
                $"modifier key caps share their row metrics at {factor:0.##}x text");
            Check(modifiers.Zip(modifiers.Skip(1)).All(pair => pair.First.Right + UiTheme.Scale(8, editor.DeviceDpi) == pair.Second.Left),
                $"modifier key caps have consistent gaps at {factor:0.##}x text");
            TextBox input = FindControls<TextBox>(editor).Single();
            Check(input.Dock == DockStyle.None && input.Parent is Panel &&
                Math.Abs(input.Top + input.Height / 2 - input.Parent.Height / 2) <= 2,
                $"shortcut input text box is vertically centered at {factor:0.##}x text");
        }
        Button[] actions = FindControls<Button>(form).Where(button => button.Parent == editors[0].Parent).ToArray();
        Check(actions.Length == 3 && actions.All(button => button.Top == actions[0].Top && button.Bottom == actions[0].Bottom),
            $"settings action buttons share top and bottom alignment at {factor:0.##}x text");
    }

    private static void SaveNativeWindowPreview(Control control, string name, uint printFlags = 2)
    {
        NativeMethods.GetWindowRect(control.Handle, out NativeMethods.Rect rect);
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        using var bitmap = new Bitmap(width, height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            IntPtr dc = graphics.GetHdc();
            try
            {
                Check(NativeMethods.PrintWindow(control.Handle, dc, printFlags),
                    "native PrintWindow captures the fully rendered dialog: " + name);
            }
            finally { graphics.ReleaseHdc(dc); }
        }
        bitmap.Save(Path.Combine(Artifacts, name), System.Drawing.Imaging.ImageFormat.Png);
    }

    private static async Task TestDialogTypography()
    {
        foreach (float factor in new[] { 1F, 1.5F, 2F })
        {
            foreach (Form form in new Form[] { new AboutForm(), new HotKeySettingsForm(HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault) })
            {
                using (form)
                {
                    // Enlarge text independently of the actual monitor DPI. This is not a simulated monitor transition.
                    foreach (Control control in FindControls<Control>(form))
                        control.Font = new Font(control.Font.FontFamily, control.Font.Size * factor, control.Font.Style);
                    form.Show();
                    await Task.Delay(40);
                    form.PerformLayout();
                    Check(!form.HorizontalScroll.Visible,
                        $"{form.GetType().Name} avoids horizontal scrolling at {factor:0.##}x text");
                    CheckVisibleLayout(form);
                    SaveControlPreview(form, $"{(form is AboutForm ? "about" : "settings")}-text-{factor * 100:0}.png");
                    if (form is AboutForm && factor == 1F)
                    {
                        Size client = form.ClientSize, outer = form.Size;
                        Rectangle area = Screen.FromControl(form).WorkingArea;
                        NativeMethods.SetWindowPos(form.Handle, IntPtr.Zero,
                            Math.Max(area.Left, area.Right - form.Width - 48), area.Top + 44, 0, 0,
                            NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
                        await Task.Delay(40);
                        Application.DoEvents();
                        Check(form.ClientSize == client && form.Size == outer && !form.AutoScroll,
                            "About native move preserves the complete fixed-dialog size without scrolling");
                        CheckVisibleLayout(form);
                        SaveNativeWindowPreview(form, "about-after-native-move.png");
                    }
                    form.Close();
                }
            }
        }
    }

    private static void CheckVisibleLayout(Form form)
    {
        foreach (Control control in FindControls<Control>(form).Where(c => c.Visible && c is Label or Button or TextBox or CheckBox))
        {
            Rectangle visible = new(form.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
            bool insideScrollingViewport = false;
            for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
                if (parent is ScrollableControl scroll && scroll.AutoScroll) insideScrollingViewport = true;
            if (insideScrollingViewport) continue;
            Check(form.ClientRectangle.Contains(visible), $"{form.GetType().Name} shows {control.Text.Split('\n')[0]} within its client area");
            if (control is CheckBox checkBox && checkBox.Appearance == Appearance.Button)
            {
                Size text = TextRenderer.MeasureText(checkBox.Text, checkBox.Font,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                Check(checkBox.ClientSize.Width >= text.Width + checkBox.Padding.Horizontal + UiTheme.Scale(20, checkBox.DeviceDpi) &&
                    checkBox.ClientSize.Height >= text.Height + checkBox.Padding.Vertical,
                    $"modifier key cap fits its complete label: {checkBox.Text}");
            }
            if (control is Label label)
            {
                Size preferred = label.GetPreferredSize(new Size(label.Width, 0));
                Check(label.Height >= preferred.Height, "manual or automatic label fits its complete text: " + label.Text.Split('\n')[0] +
                    $" (size {label.Size}, preferred {preferred}, font {label.Font.Size:0.##})");
            }
        }
    }

    private static async Task TestPopupEdges()
    {
        var store = new ConfigStore(Path.Combine(Artifacts, "ui-edges.json"));
        store.TrySave(new AppConfig { Shortcut = TestBinding(Keys.F7), ScreenshotShortcut = TestBinding(Keys.F8) }, out _);
        using var context = new TrayApplicationContext(store);
        try
        {
            ContextMenuStrip menu = Field<ContextMenuStrip>(context, "_menu");
            ToolStripMenuItem managed = Field<ToolStripMenuItem>(context, "_managedWindowsMenu");
            Rectangle area = Screen.PrimaryScreen!.WorkingArea;
            menu.Show(area.Right - 4, area.Bottom - 4);
            await Task.Delay(40);
            Check(area.Contains(menu.Bounds), "tray menu fits at the bottom-right screen edge");
            ToolStripMenuItem[] visibleRows = menu.Items.OfType<ToolStripMenuItem>().Where(item => item.Visible).ToArray();
            Check(visibleRows.Where(item => !string.IsNullOrEmpty(item.Text ?? string.Empty)).All(item => item.Height <= UiTheme.Scale(36, menu.DeviceDpi) + 1),
                "tray menu rows stay within the compact 32–36 logical-pixel rhythm");
            Invoke(context, "ShowMenuError", MenuErrorKind.ScreenshotShortcut,
                "截图贴图快捷键 Ctrl + Shift + S 不可用（Windows 错误 1409）。点击打开快捷键设置。", null!);
            await Task.Delay(20);
            SaveControlPreview(menu, "tray-error.png");
            ToolStripMenuItem feedback = Field<ToolStripMenuItem>(context, "_feedbackItem");
            Check(feedback.Visible && feedback.Text == "截图贴图快捷键不可用" && (feedback.ToolTipText ?? string.Empty).Contains("1409"),
                "tray error preview exposes a concrete operation and full reason");
            Invoke(context, "ClearMenuError");
            managed.ShowDropDown();
            await Task.Delay(40);
            ToolStripDropDown dropDown = managed.DropDown;
            Check(area.Contains(dropDown.Bounds), "submenu fits at the bottom-right screen edge");
            SaveControlPreview(dropDown, "submenu-edge.png");
            managed.HideDropDown();
            for (int i = 0; i < 80; i++)
            {
                var item = new ToolStripMenuItem($"测试窗口 {i + 1:00} · 较长标题验证菜单滚动") { Image = Field<Image>(context, "_pinMenuImage") };
                UiTheme.StyleMenuItem(item);
                managed.DropDownItems.Add(item);
            }
            managed.ShowDropDown();
            await Task.Delay(80);
            Check(area.Contains(managed.DropDown.Bounds), "long window list stays within the monitor and uses native menu scrolling");
            SaveControlPreview(managed.DropDown, "submenu-long-list.png");
            managed.HideDropDown();
            menu.Close();
            menu.Show(100, 100);
            await Task.Delay(40);
            ToolStripMenuItem capture = Field<ToolStripMenuItem>(context, "_captureItem");
            capture.Select();
            SaveControlPreview(menu, "tray-focus.png");
            menu.Close();
        }
        finally { Invoke(context, "ExitThreadCore"); }
    }

    private static async Task TestTrayPopover()
    {
        using var popover = new TrayPopoverForm(
            () => { }, () => { }, _ => { }, () => { }, () => { },
            () => { }, () => { }, () => { }, () => { }, () => { });
        Rectangle area = Screen.PrimaryScreen!.WorkingArea;
        var emptySnapshot = new TrayPopoverSnapshot(
            Array.Empty<ManagedWindow>(), 0, false,
            HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault,
            null, null, false);
        popover.ShowAtTray(new Point(area.Right - 40, area.Bottom - 2), emptySnapshot);
        await Task.Delay(50);
        int margin = UiTheme.Scale(12, popover.DeviceDpi);
        Check(area.Contains(popover.Bounds) &&
            popover.Right == area.Right - margin && popover.Bottom == area.Bottom - margin,
            "tray popover anchors cleanly at the bottom-right corner of the monitor working area");
        SaveControlPreview(popover, "tray-popover-empty.png");

        var activeWindows = new[]
        {
            new ManagedWindow((IntPtr)1, 1, 1, "Chrome_WidgetWin_1", "设计规范与需求文档 - Visual Studio Code", DateTime.Now),
            new ManagedWindow((IntPtr)2, 1, 2, "CASCADIA_HOSTING_WINDOW_CLASS", "Windows PowerShell · TopMostTool", DateTime.Now),
            new ManagedWindow((IntPtr)3, 1, 3, "CabinetWClass", "win-x64 - 文件资源管理器", DateTime.Now)
        };
        var activeSnapshot = new TrayPopoverSnapshot(
            activeWindows.Take(2).ToArray(), 2, true,
            HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault,
            null, null, false);
        popover.UpdateSnapshot(activeSnapshot);
        await Task.Delay(50);
        Check(area.Contains(popover.Bounds) && popover.Bottom == area.Bottom - margin,
            "populated tray popover stays anchored at bottom-right");
        SaveControlPreview(popover, "tray-popover-active.png");

        var threeRowSnapshot = new TrayPopoverSnapshot(
            activeWindows, 2, true,
            HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault,
            null, null, false);
        popover.UpdateSnapshot(threeRowSnapshot);
        int threeRowHeight = popover.Height;

        var manyWindows = Enumerable.Range(1, 8)
            .Select(i => new ManagedWindow((IntPtr)i, 1, (uint)i, "App", $"置顶工作窗口 #{i:00} - 示例项目文档", DateTime.Now))
            .ToArray();
        popover.UpdateSnapshot(new TrayPopoverSnapshot(
            manyWindows, 3, true,
            HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault,
            null, null, false));
        await Task.Delay(50);
        Check(popover.Height == threeRowHeight && popover.Bottom == area.Bottom - margin,
            $"many pinned windows cap popover height at 3 visible rows ({popover.Height}px == {threeRowHeight}px) and keep bottom-right anchor");
        SaveControlPreview(popover, "tray-popover-many.png");

        var errorSnapshot = new TrayPopoverSnapshot(
            activeWindows.Take(1).ToArray(), 0, false,
            HotKeyBinding.Default, HotKeyBinding.ScreenshotDefault,
            "截图贴图快捷键不可用", "截图贴图快捷键 Ctrl + Shift + S 被占用（错误 1409）", true);
        popover.UpdateSnapshot(errorSnapshot);
        await Task.Delay(50);
        SaveControlPreview(popover, "tray-popover-error.png");

        Invoke(popover, "OnKeyDown", new KeyEventArgs(Keys.Escape));
        Check(!popover.Visible, "Escape hides the tray control-center popover");
    }

    private static async Task TestImageKeyboard()
    {
        using var image = new PinnedImageWindow(CreatePattern(), new Point(200, 200), 7);
        image.Show();
        await Task.Delay(40);
        Invoke(image, "ProcessCmdKey", Message.Create(image.Handle, 0, IntPtr.Zero, IntPtr.Zero), Keys.Control | Keys.Oemplus);
        Check(image.Zoom > 1, "Ctrl plus enlarges the focused pinned image");
        Invoke(image, "ProcessCmdKey", Message.Create(image.Handle, 0, IntPtr.Zero, IntPtr.Zero), Keys.Control | Keys.D0);
        Check(Math.Abs(image.Zoom - 1) < 0.001, "Ctrl zero resets image zoom");
        FindControls<Button>(image).Single(button => button.AccessibleName == "复制贴图").PerformClick();
        Check(FindControls<Label>(image).Any(label => label.Text.Contains("已复制")), "copy displays confirmation directly in the image toolbar");
        SaveControlPreview(image, "pinned-image-copied.png");
        image.ContextMenuStrip!.Show(image, new Point(30, 40));
        await Task.Delay(40);
        image.ContextMenuStrip.Close();
        await Task.Delay(40);
        Check(!image.ContextMenuStrip.Visible && !image.IsDisposed, "dismissing the screenshot menu keeps the image open");
        image.Close();
    }
}
