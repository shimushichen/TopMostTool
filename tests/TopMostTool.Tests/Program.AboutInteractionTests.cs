using TopMostTool;

internal static partial class Program
{
    private static async Task TestAboutInteraction()
    {
        var store = new ConfigStore(Path.Combine(Artifacts, "about-interaction-config.json"));
        Check(store.TrySave(new AppConfig
        {
            Shortcut = TestBinding(Keys.F7), ScreenshotShortcut = TestBinding(Keys.F8), ShowNotifications = false
        }, out _), "About interaction uses isolated shortcut settings");
        using var context = new TrayApplicationContext(store);
        try
        {
            Rectangle workArea = Screen.PrimaryScreen!.WorkingArea;
            Invoke(context, "AddPinnedImage", CreatePattern(), new Point(workArea.Left + 40, workArea.Top + 140));
            PinnedImageWindow image = Field<List<PinnedImageWindow>>(context, "_screenshots").Single();

            // Ensure the old ShowDialog implementation fails without hanging the suite.
            using var modalGuard = new System.Windows.Forms.Timer { Interval = 1000 };
            modalGuard.Tick += (_, _) =>
            {
                foreach (AboutForm dialog in Application.OpenForms.OfType<AboutForm>().ToArray()) dialog.Close();
            };
            modalGuard.Start();
            try { Invoke(context, "ShowAbout"); }
            finally { modalGuard.Stop(); }
            AboutForm? about = Application.OpenForms.OfType<AboutForm>().SingleOrDefault();
            Check(about is { Visible: true, Modal: false }, "opening About returns while the nonmodal page remains visible");
            Check(IsWindowEnabled(image.Handle), "an existing screenshot remains natively enabled while About is open");
            Invoke(context, "ShowAbout");
            Check(Application.OpenForms.OfType<AboutForm>().Single() == about, "repeated About requests reuse one window");

            image.Activate();
            await ClickControl(FindControls<Button>(image).Single(button => button.AccessibleName == "复制贴图"));
            string copyFeedback = FindControls<Label>(image).Single(label => label.AccessibleName == "贴图状态").Text;
            Check(copyFeedback.Contains("已复制") || copyFeedback.StartsWith("复制失败："),
                "native Copy button dispatch executes and reports the clipboard result while About is open");
            Console.WriteLine($"Copy result: {copyFeedback}");

            await ClickControl(FindControls<Button>(image).Single(button => button.AccessibleName == "放大"));
            Check(image.Zoom > 1, "toolbar zoom responds while About is open");
            await ClickControl(FindControls<Button>(image).Single(button => button.AccessibleName == "重置缩放"));
            Check(Math.Abs(image.Zoom - 1) < 0.001, "toolbar reset responds while About is open");

            Point menuPoint = image.PointToScreen(new Point(80, image.ImageRectangle.Top + 40));
            NativeMethods.SendMessage(image.Handle, 0x007B, image.Handle,
                (IntPtr)((menuPoint.Y << 16) | (menuPoint.X & 0xffff)));
            await Until(() => image.ContextMenuStrip!.Visible, "native context-menu dispatch opens the screenshot menu alongside About");
            image.ContextMenuStrip!.Close();

            bool saveOpened = false;
            using (var cancelSave = new System.Windows.Forms.Timer { Interval = 50 })
            {
                cancelSave.Tick += (_, _) =>
                {
                    IntPtr dialog = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "#32770", "保存截图贴图");
                    if (dialog == IntPtr.Zero) return;
                    NativeMethods.GetWindowThreadProcessId(dialog, out uint processId);
                    if (processId != Environment.ProcessId) return;
                    saveOpened = true;
                    PostMessage(dialog, 0x0010, IntPtr.Zero, IntPtr.Zero);
                };
                cancelSave.Start();
                await ClickControl(FindControls<Button>(image).Single(button => button.AccessibleName == "保存贴图"));
                await Until(() => saveOpened && IsWindowEnabled(image.Handle), "Save opens its picker and cancellation restores screenshot interaction");
            }
            Check(about!.Visible && IsWindowEnabled(about.Handle), "About remains usable after the screenshot save picker closes");

            SaveControlPreview(image, "pinned-image-about-open.png");

            Invoke(context, "AddPinnedImage", CreatePattern(), new Point(workArea.Left + 180, workArea.Top + 240));
            PinnedImageWindow second = Field<List<PinnedImageWindow>>(context, "_screenshots").Last();
            Check(IsWindowEnabled(second.Handle), "a screenshot created after About also remains enabled");
            await ClickControl(FindControls<Button>(second).Single(button => button.AccessibleName == "关闭贴图"));
            Check(second.IsDisposed && about.Visible, "native Close button dispatch closes only the selected screenshot");

            FindControls<Button>(about).Single(button => button.Text == "关闭").PerformClick();
            Check(about.IsDisposed && image.Visible, "the About Close button disposes its nonmodal window without closing screenshots");
            Invoke(context, "ShowAbout");
            AboutForm reopened = Application.OpenForms.OfType<AboutForm>().Single();
            Check(reopened != about, "About can reopen after being disposed");
            Invoke(reopened, "ProcessDialogKey", Keys.Escape);
            Check(reopened.IsDisposed, "Escape closes the nonmodal About page");
            Invoke(context, "ShowAbout");
            reopened = Application.OpenForms.OfType<AboutForm>().Single();
            Invoke(reopened, "ProcessDialogKey", Keys.Enter);
            Check(reopened.IsDisposed, "Enter closes the nonmodal About page");

            Invoke(context, "ShowAbout");
            reopened = Application.OpenForms.OfType<AboutForm>().Single();
            Invoke(context, "ExitThreadCore");
            Check(reopened.IsDisposed && image.IsDisposed, "application exit closes both About and screenshot windows");
            Invoke(context, "ShowAbout");
            Check(!Application.OpenForms.OfType<AboutForm>().Any(), "About cannot reopen during application shutdown");
        }
        finally
        {
            Invoke(context, "ExitThreadCore");
        }
    }

    private static async Task ClickControl(Control control)
    {
        // Deliver BM_CLICK through the native button window; physical mouse
        // injection is unavailable in some unattended Windows sessions.
        NativeMethods.SendMessage(control.Handle, 0x00F5, IntPtr.Zero, IntPtr.Zero);
        await Task.Delay(40);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr window);
}
