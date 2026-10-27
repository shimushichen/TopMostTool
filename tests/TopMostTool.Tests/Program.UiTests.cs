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
            form.Location = new Point(-10000, -10000);
            form.Show();
            await Task.Delay(30);
            form.PerformLayout();
            HotKeyEditor[] editors = FindControls<HotKeyEditor>(form).ToArray();
            Check(editors.Length == 2, $"compact settings layout has two cards at {factor:0.##}x text");
            int gap = editors[1].Top - editors[0].Bottom;
            Check(gap <= UiTheme.Scale(16, form.DeviceDpi) + 2,
                $"shortcut cards keep a compact gap at {factor:0.##}x text ({gap}px)");
            Check(editors.All(editor => editor.Height <= UiTheme.Scale(190, form.DeviceDpi) * factor + 12),
                $"shortcut cards shrink to their content at {factor:0.##}x text");
            CheckVisibleLayout(form);
            SaveControlPreview(form, factor == 1F ? "shortcut-settings-compact.png" : $"shortcut-settings-compact-{factor:0.##}.png");
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
        SaveControlPreview(errorForm, "shortcut-settings-compact-error.png");
        CheckVisibleLayout(errorForm);
        errorForm.Close();
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
                    CheckVisibleLayout(form);
                    SaveControlPreview(form, $"{(form is AboutForm ? "about" : "settings")}-text-{factor * 100:0}.png");
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
            if (control.Parent is ScrollableControl scroll && scroll.AutoScroll) continue;
            Check(form.ClientRectangle.Contains(visible), $"{form.GetType().Name} shows {control.Text.Split('\n')[0]} within its client area");
            if (control is Label label && label.AutoSize)
            {
                Size preferred = label.GetPreferredSize(new Size(label.Width, 0));
                Check(label.Height >= preferred.Height, "auto-sized label fits enlarged text: " + label.Text.Split('\n')[0]);
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
