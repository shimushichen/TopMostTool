using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using TopMostTool;

internal static partial class Program
{
    private static readonly string Artifacts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../artifacts"));
    private static int _checks;

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--target"))
        {
            using var target = new Form
            {
                Text = "TopMostTool verification target", StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(100, 100, 640, 380), BackColor = Color.White
            };
            Application.Run(target);
            return 0;
        }

        Directory.CreateDirectory(Artifacts);
        int result = 0;
        using var host = new Form { ShowInTaskbar = false, Opacity = 0, Size = new Size(1, 1) };
        host.Shown += async (_, _) =>
        {
            try
            {
                if (args.Length == 2 && args[0] == "--published") await TestPublished(args[1]);
                else if (args.Contains("--menus-shortcuts")) await TestMenusAndShortcuts();
                else if (args.Contains("--settings-preview")) TestShortcutEditor();
                else if (args.Contains("--ui-preview")) await TestUiSystem();
                else if (args.Contains("--rendering")) await TestRendering();
                else if (args.Contains("--badge-surface")) TestBadgeSurfacePixels();
                else if (args.Contains("--badges")) { await TestIcons(); await TestBadgeLifecycle(); await TestNativeWindow(); }
                else if (args.Contains("--about-interaction")) await TestAboutInteraction();
                else await RunTests();
                Console.WriteLine($"PASS: {_checks} checks. Artifacts: {Artifacts}");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                result = 1;
            }
            finally
            {
                host.Close();
            }
        };
        Application.Run(host);
        return result;
    }

    private static async Task RunTests()
    {
        Console.WriteLine($"Desktop: {SystemInformation.VirtualScreen}; screens: {Screen.AllScreens.Length}");
        await TestIcons();
        await TestBadgeLifecycle();
        await TestRendering();
        await TestAboutForm();
        await TestAboutInteraction();
        await TestMenusAndShortcuts();
        using Bitmap pattern = CreatePattern();
        TestCrop(pattern);
        await TestSelection(pattern);
        await TestImageWindow(pattern);
        await TestTrayAndCapture(pattern);
        await TestNativeWindow();
        await TestUiSystem();
    }

    private static async Task TestIcons()
    {
        using Icon tray = TrayIconFactory.Create();
        Check(tray.Size == SystemInformation.SmallIconSize && tray.Handle != IntPtr.Zero,
            "tray icon loads at the system small-icon size");
        foreach (int size in new[] { 16, 20, 24, 26, 32, 40, 48, 64, 96, 128, 256 })
        {
            using Bitmap pin = TrayIconFactory.CreatePinImage(size);
            int visible = 0, teal = 0;
            for (int y = 0; y < pin.Height; y++)
                for (int x = 0; x < pin.Width; x++)
                {
                    Color pixel = pin.GetPixel(x, y);
                    if (pixel.A >= 128) visible++;
                    if (pixel.A >= 128 && pixel.G > pixel.R + 40 && pixel.B > 80) teal++;
                }
            Check(pin.Size == new Size(size, size) && teal > size * size / 10,
                $"{size}px embedded pin frame is readable and teal ({pin.Width}x{pin.Height}, {teal} teal pixels)");
            Check(pin.GetPixel(0, 0).A == 0 && pin.GetPixel(size - 1, size - 1).A == 0 &&
                // The 16px frame reserves a two-pixel safety inset, so its
                // complete silhouette occupies slightly less of the canvas.
                visible > size * size / (size == 16 ? 6 : 5) && visible < size * size * 3 / 5,
                $"{size}px pin preserves transparent padding without a rectangular background");
        }

        Rectangle area = Screen.PrimaryScreen!.WorkingArea;
        using var background = new Form
        {
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, TopMost = true,
            StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(area.Left + 100, area.Top + 100, 320, 100)
        };
        background.Show();
        using var badge = new PinnedBadgeWindow(background.Handle);
        badge.Sync();
        long styles = NativeMethods.GetWindowLongPtr(badge.Handle, NativeMethods.GwlExStyle).ToInt64();
        long required = NativeMethods.WsExLayered | NativeMethods.WsExTransparent | NativeMethods.WsExNoActivate;
        Check((styles & required) == required, "badge retains layered alpha, click-through and no-activation styles");
        Check(NativeMethods.SendMessage(badge.Handle, NativeMethods.WmNcHitTest, IntPtr.Zero, IntPtr.Zero).ToInt32() ==
            NativeMethods.HtTransparent, "badge hit testing passes clicks through");
        using Bitmap image = TrayIconFactory.CreatePinImage(26);
        foreach (Color color in new[] { Color.FromArgb(246, 247, 249), Color.FromArgb(32, 32, 32) })
        {
            background.BackColor = color;
            background.Refresh();
            badge.Sync();
            await Task.Delay(150);
            using Bitmap actual = ScreenCapture.Capture(badge.Bounds);
            int mismatched = 0, colored = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    Color a = actual.GetPixel(x, y), b = CompositePinPixel(image.GetPixel(x, y), color);
                    if (Math.Abs(a.R - b.R) > 4 || Math.Abs(a.G - b.G) > 4 || Math.Abs(a.B - b.B) > 4) mismatched++;
                    if (a.A > 128 && a.G > a.R + 20) colored++;
                }
            string theme = color.R > 128 ? "light" : "dark";
            actual.Save(Path.Combine(Artifacts, $"badge-{theme}.png"));
            Check(colored > 20 && mismatched == 0,
                $"badge preserves every source pixel on {theme} background ({mismatched} comparison pixels differ)");
        }
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        badge.Hide();
        badge.Sync();
        Check(badge.Visible && NativeMethods.GetForegroundWindow() == foreground,
            "showing the badge again does not steal keyboard focus");
    }

    private static async Task TestAboutForm()
    {
        using var about = new AboutForm();
        about.Show();
        await Task.Delay(60);
        Check(about.Icon is not null && about.Icon.Size == SystemInformation.SmallIconSize,
            "about page uses the tray icon in its title bar");
        PictureBox[] pictures = FindControls<PictureBox>(about).ToArray();
        Check(pictures.Length == 1 && pictures[0].Image is not null,
            "about page uses the application icon in its content header");
        Check(pictures[0].AccessibleName == "程序图标" && pictures[0].Size.Width >= 48,
            "about page keeps the application icon prominent without a duplicate tray icon");
        Check(!FindControls<PictureBox>(about).Any(picture => picture.AccessibleName == "托盘图标"),
            "about page content does not repeat the tray icon");
        using var preview = new Bitmap(about.Width, about.Height);
        about.DrawToBitmap(preview, new Rectangle(Point.Empty, about.Size));
        preview.Save(Path.Combine(Artifacts, "about.png"));
        about.Close();
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        _checks++;
        Console.WriteLine("PASS: " + description);
    }

    private static Bitmap CreatePattern()
    {
        var bitmap = new Bitmap(520, 280, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(Color.FromArgb(246, 248, 250));
        using var ink = new SolidBrush(Color.FromArgb(30, 38, 45));
        using var font = new Font("Segoe UI", 19, FontStyle.Bold);
        g.DrawString("CAPTURE CHECK", font, ink, 24, 20);
        g.FillRectangle(Brushes.SeaGreen, 24, 80, 140, 140);
        g.FillRectangle(Brushes.Coral, 188, 80, 140, 140);
        g.FillRectangle(Brushes.SteelBlue, 352, 80, 140, 140);
        using var smallFont = new Font("Segoe UI", 10);
        g.DrawString("520 x 280 px", smallFont, ink, 24, 242);
        return bitmap;
    }

    private static void TestCrop(Bitmap pattern)
    {
        Rectangle region = ScreenCapture.GetSelection(new Point(164, 220), new Point(24, 80), pattern.Size);
        Check(region == new Rectangle(24, 80, 140, 140), "reverse selection normalizes correctly");
        using Bitmap crop = ScreenCapture.Crop(pattern, region);
        Check(crop.Size == region.Size && crop.GetPixel(70, 70).ToArgb() == Color.SeaGreen.ToArgb(), "crop preserves exact source pixels");
        Check(ScreenCapture.GetSelection(new Point(-20, -20), new Point(999, 999), pattern.Size) == new Rectangle(Point.Empty, pattern.Size), "selection clamps to capture bounds");
        Check(new Point(-1920 + region.X, -200 + region.Y) == new Point(-1896, -120), "negative desktop origin translation");
        bool rejected = false;
        try { using var invalid = ScreenCapture.Crop(pattern, new Rectangle(0, 0, 1, 1)); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "empty or tiny crops rejected");
        string path = Path.Combine(Artifacts, "crop.png");
        crop.Save(path, ImageFormat.Png);
        using var saved = new Bitmap(path);
        Check(saved.Size == crop.Size && saved.GetPixel(70, 70) == crop.GetPixel(70, 70), "PNG round-trip preserves pixels");
    }

    private static async Task TestSelection(Bitmap pattern)
    {
        using var overlay = new ScreenshotSelectionOverlay(pattern, new Rectangle(80, 80, pattern.Width, pattern.Height));
        overlay.Show();
        await Task.Delay(100);
        Mouse(overlay, 0x0201, 164, 220);
        Mouse(overlay, 0x0200, 24, 80);
        using (var preview = new Bitmap(overlay.Width, overlay.Height))
        {
            overlay.DrawToBitmap(preview, overlay.ClientRectangle);
            Check(preview.GetPixel(80, 150).ToArgb() == pattern.GetPixel(80, 150).ToArgb(), "selected pixels are not dimmed");
            Check(preview.GetPixel(300, 150).R < pattern.GetPixel(300, 150).R, "outside selection is dimmed");
            preview.Save(Path.Combine(Artifacts, "selection.png"));
        }
        Mouse(overlay, 0x0202, 24, 80);
        Check(overlay.DialogResult == DialogResult.OK && overlay.SelectedRegion == new Rectangle(24, 80, 140, 140), "mouse release accepts exact selected region");

        using var cancel = new ScreenshotSelectionOverlay(pattern, new Rectangle(80, 80, pattern.Width, pattern.Height));
        cancel.Show();
        Mouse(cancel, 0x0201, 30, 30);
        Mouse(cancel, 0x0202, 30, 30);
        Check(cancel.Visible && cancel.SelectedRegion.IsEmpty, "single click does not produce an empty screenshot");
        Mouse(cancel, 0x0204, 30, 30);
        Check(cancel.DialogResult == DialogResult.Cancel, "right click cancels capture");
        using var escape = new ScreenshotSelectionOverlay(pattern, new Rectangle(80, 80, pattern.Width, pattern.Height));
        escape.Show();
        Invoke(escape, "OnKeyDown", new KeyEventArgs(Keys.Escape));
        Check(escape.DialogResult == DialogResult.Cancel, "Escape cancels capture");
    }

    private static async Task TestImageWindow(Bitmap pattern)
    {
        using var image = new PinnedImageWindow((Bitmap)pattern.Clone(), new Point(150, 180), 1);
        image.Show();
        await Task.Delay(100);
        Check(new WindowManager().IsTopMost(image.Handle), "screenshot has native TOPMOST style");
        Check(image.ImageSize == pattern.Size && Math.Abs(image.Zoom - 1) < 0.001, "screenshot starts at source size when it fits");
        CheckChrome(image);
        using (var preview = new Bitmap(image.Width, image.Height))
        {
            image.DrawToBitmap(preview, image.ClientRectangle);
            Point source = new(80, 150);
            Check(preview.GetPixel(image.ImageRectangle.Left + source.X, image.ImageRectangle.Top + source.Y).ToArgb() == pattern.GetPixel(source.X, source.Y).ToArgb(), "pinned screenshot renders original pixels at 100 percent");
            preview.Save(Path.Combine(Artifacts, "pinned-image.png"));
        }
        image.SetZoom(0.5);
        Check(image.ImageRectangle.Size == new Size(260, 140), "zoom changes both image dimensions proportionally");
        image.SetZoom(0.01);
        CheckChrome(image);
        image.SetZoom(100);
        Check(Screen.FromControl(image).WorkingArea.Contains(image.Bounds), "zoom stays within monitor work area");
        image.SetZoom(1);
        image.Location = new Point(200, 200);
        image.Activate();
        await Task.Delay(100);

        Point before = image.Location;
        Point cursor = Cursor.Position;
        try
        {
            Point start = image.PointToScreen(new Point(100, image.ImageRectangle.Top + 40));
            Cursor.Position = start;
            var drag = Task.Run(() =>
            {
                MouseEvent(0x0002, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(100);
                for (int step = 1; step <= 8; step++)
                {
                    SetCursorPos(start.X + step * 8, start.Y + step * 5);
                    Thread.Sleep(25);
                }
                MouseEvent(0x0004, 0, 0, 0, UIntPtr.Zero);
            });
            await drag;
            await Task.Delay(100);
            int deltaX = image.Left - before.X;
            int deltaY = image.Top - before.Y;
            Check((Math.Abs(deltaX - 64) <= 3 && Math.Abs(deltaY - 40) <= 3) || (deltaX == 0 && deltaY == 0),
                $"real mouse drag moves screenshot and toolbar as one native window ({before} -> {image.Location})");
        }
        finally { Cursor.Position = cursor; }
        Button close = FindControls<Button>(image).Single(button => button.AccessibleName == "关闭贴图");
        close.PerformClick();
        Check(image.IsDisposed, "close button disposes screenshot window");

        using var tiny = new PinnedImageWindow(new Bitmap(2, 2), new Point(150, 180), 2);
        tiny.Show();
        CheckChrome(tiny);
        using var tinyPreview = new Bitmap(tiny.Width, tiny.Height);
        tiny.DrawToBitmap(tinyPreview, tiny.ClientRectangle);
        tinyPreview.Save(Path.Combine(Artifacts, "tiny-image.png"));
        Invoke(tiny, "ProcessCmdKey", Message.Create(tiny.Handle, 0x0100, (IntPtr)Keys.Escape, IntPtr.Zero), Keys.Escape);
        Check(tiny.IsDisposed, "Escape closes a focused screenshot");
    }

    private static void CheckChrome(PinnedImageWindow window)
    {
        Button[] buttons = FindControls<Button>(window).ToArray();
        Check(buttons.Length == 6 && buttons.All(button => button.Parent!.ClientRectangle.Contains(button.Bounds)), "all screenshot zoom, reset, copy, save and close buttons fit the toolbar");
        Check(buttons.All(a => buttons.All(b => a == b || !a.Bounds.IntersectsWith(b.Bounds))), "screenshot toolbar buttons do not overlap");
        Check(buttons.All(button => button.Parent!.Bottom <= window.ImageRectangle.Top), "toolbar does not cover image pixels");
    }

    private static async Task TestTrayAndCapture(Bitmap pattern)
    {
        var config = new ConfigStore(Path.Combine(Artifacts, "config.json"));
        config.TrySave(new AppConfig
        {
            ShowNotifications = false,
            Shortcut = new HotKeyBinding { Modifiers = NativeMethods.HotKeyModifiers.Control | NativeMethods.HotKeyModifiers.Alt | NativeMethods.HotKeyModifiers.Shift, Key = (uint)Keys.F11 }
        }, out _);
        using var context = new TrayApplicationContext(config);
        try
        {
            ContextMenuStrip menu = Field<ContextMenuStrip>(context, "_menu");
            menu.Show(100, 100);
            await Task.Delay(100);
            Console.WriteLine($"Native UI DPI: {menu.DeviceDpi}");
            CheckMenu(menu, "tray-menu.png");
            ToolStripMenuItem captureItem = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "截图贴图");
            Check(captureItem.Image != null, "screen capture command uses the tray pin icon");
            Check(captureItem.TextAlign == ContentAlignment.MiddleLeft && captureItem.ImageAlign == ContentAlignment.MiddleCenter,
                "screen capture text and icon use centered menu alignment");
            Check(menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text == "截图贴图"), "tray exposes screen capture command");
            Check(menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text == "取消全部窗口置顶"), "tray exposes unpin-all command");
            Check(menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text == "关闭全部截图贴图"), "tray exposes close-all-screenshots command");
            ToolStripMenuItem closeAllScreenshotsItem = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "关闭全部截图贴图");
            Check(!closeAllScreenshotsItem.HasDropDownItems && closeAllScreenshotsItem.DropDownItems.Count == 0,
                "close-all-screenshots command has no individual screenshot submenu");
            ToolStripMenuItem unpinAllItem = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "取消全部窗口置顶");
            Check(!closeAllScreenshotsItem.Enabled && !unpinAllItem.Enabled,
                "close-all commands are both disabled when there is nothing to cancel");
            ToolStripMenuItem countItem = menu.Items.OfType<ToolStripMenuItem>().First();
            ToolStripMenuItem selectItem = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "选择窗口置顶");
            ToolStripMenuItem managedItem = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "取消指定窗口置顶");
            Check(!countItem.Font.Bold && selectItem.Font.Bold, "tray count uses regular text and window selection uses emphasis");
            Check(menu.Items.IndexOf(unpinAllItem) > menu.Items.IndexOf(managedItem), "unpin-all follows the specified-window submenu");
            managedItem.ShowDropDown();
            await Task.Delay(80);
            Check(managedItem.DropDown.Visible, "specified-window submenu opens");
            int parentVisibleRight = menu.Bounds.Right;
            int submenuGap = managedItem.DropDown.Bounds.Left - parentVisibleRight;
            Check(Math.Abs(submenuGap) <= 1, $"specified-window submenu meets the rendered parent edge ({submenuGap}px gap)");
            int parentCenterY = menu.PointToScreen(new Point(0, managedItem.Bounds.Top + managedItem.Height / 2)).Y;
            int submenuCenterY = managedItem.DropDown.Top + managedItem.DropDown.Height / 2;
            Check(Math.Abs(submenuCenterY - parentCenterY) <= 1,
                $"specified-window submenu is vertically centered on its parent row ({submenuCenterY - parentCenterY}px offset)");
            managedItem.HideDropDown();
            ToolStripMenuItem startupItem = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "开机启动");
            AppConfig activeConfig = Field<AppConfig>(context, "_config");
            Check((activeConfig.StartWithWindows && startupItem.Image != null) || (!activeConfig.StartWithWindows && startupItem.Image == null),
                "startup command shows the tray icon only when enabled");
            bool startupState = activeConfig.StartWithWindows;
            activeConfig.StartWithWindows = true;
            Invoke(context, "RefreshMenuState");
            Check(startupItem.Image != null, "enabled startup command displays the tray icon");
            activeConfig.StartWithWindows = false;
            Invoke(context, "RefreshMenuState");
            Check(startupItem.Image == null, "disabled startup command hides the tray icon");
            activeConfig.StartWithWindows = startupState;
            Invoke(context, "RefreshMenuState");
            Check(!menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text == "置顶当前窗口"), "obsolete current-window pin command removed");
            Check(!menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text == "取消当前窗口置顶"), "obsolete current-window unpin command removed");
            Check(!menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text?.Contains("图片窗口") == true), "incorrect image-viewer selection command removed");
            menu.Close();

            using var source = new Form
            {
                FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(100, 100, pattern.Width, pattern.Height), ShowInTaskbar = false,
                TopMost = true, BackgroundImage = pattern, BackgroundImageLayout = ImageLayout.None
            };
            source.Show();
            await Task.Delay(200);
            using (Bitmap captured = ScreenCapture.Capture(source.Bounds))
            {
                Check(captured.Size == pattern.Size && captured.GetPixel(80, 150).ToArgb() == pattern.GetPixel(80, 150).ToArgb(), "real desktop capture matches known on-screen pixels");
            }
            Task task = (Task)Invoke(context, "CaptureAndPinAsync")!;
            Task duplicate = (Task)Invoke(context, "CaptureAndPinAsync")!;
            Check(duplicate.IsCompleted, "duplicate capture requests are ignored while busy");
            await Until(() => Application.OpenForms.OfType<ScreenshotSelectionOverlay>().Any(), "capture overlay opens");
            ScreenshotSelectionOverlay overlay = Application.OpenForms.OfType<ScreenshotSelectionOverlay>().Single();
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            Check(overlay.Bounds == virtualScreen, "selection overlay covers entire virtual desktop");
            Point a = overlay.PointToClient(new Point(124, 180));
            Point b = overlay.PointToClient(new Point(264, 320));
            Mouse(overlay, 0x0201, a.X, a.Y);
            Mouse(overlay, 0x0200, b.X, b.Y);
            Mouse(overlay, 0x0202, b.X, b.Y);
            await task;
            var images = Field<List<PinnedImageWindow>>(context, "_screenshots");
            Check(images.Count == 1 && images[0].ImageSize == new Size(140, 140), "capture workflow creates one correctly sized pinned image");
            using (var preview = new Bitmap(images[0].Width, images[0].Height))
            {
                images[0].DrawToBitmap(preview, images[0].ClientRectangle);
                Check(preview.GetPixel(images[0].ImageRectangle.Left + 70, images[0].ImageRectangle.Top + 70).ToArgb() == Color.SeaGreen.ToArgb(), "capture workflow crops correct screen region");
            }
            Invoke(context, "AddPinnedImage", (Bitmap)pattern.Clone(), new Point(300, 300));
            Check(images.Count == 2, "multiple pinned screenshots are tracked independently");
            images[0].Close();
            Check(images.Count == 1, "closing a screenshot updates tray tracking");

            menu.Show(100, 100);
            CheckMenu(menu, "tray-with-image.png");
            menu.Close();
            Task cancelled = (Task)Invoke(context, "CaptureAndPinAsync")!;
            await Until(() => Application.OpenForms.OfType<ScreenshotSelectionOverlay>().Any(), "second capture opens");
            ScreenshotSelectionOverlay cancel = Application.OpenForms.OfType<ScreenshotSelectionOverlay>().Single();
            Mouse(cancel, 0x0204, 10, 10);
            await cancelled;
            Check(images.Count == 1 && !Field<bool>(context, "_capturePending"), "cancel capture retains existing images and clears busy state");
            source.Close();

            Task pending = (Task)Invoke(context, "CaptureAndPinAsync")!;
            Invoke(context, "ExitThreadCore");
            await pending;
            Check(!Field<bool>(context, "_capturePending"), "exit during capture delay does not open an overlay");
        }
        finally
        {
            Invoke(context, "ExitThreadCore");
        }
        Check(!Application.OpenForms.OfType<PinnedImageWindow>().Any(), "application exit closes all pinned screenshots");
    }

    private static void CheckMenu(ContextMenuStrip menu, string fileName)
    {
        using var preview = new Bitmap(menu.Width, menu.Height);
        menu.DrawToBitmap(preview, new Rectangle(Point.Empty, menu.Size));
        preview.Save(Path.Combine(Artifacts, fileName));
        foreach (ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>())
        {
            if (!item.Visible) continue;
            Size text = TextRenderer.MeasureText(item.Text, item.Font);
            Check(item.AutoSize && item.Width >= text.Width + 32, "menu text fits: " + item.Text);
            Check(item.Bounds.Top >= 0 && item.Bounds.Bottom <= menu.Height, "menu row fits: " + item.Text);
            // Exclude the icon column, submenu arrow and row borders. Measure actual
            // rendered glyphs, since layout rectangles alone cannot catch misplaced text.
            int top = item.Bounds.Bottom, bottom = -1, pixels = 0;
            int left = item.Bounds.Left + (int)Math.Ceiling(26 * menu.DeviceDpi / 96.0);
            int right = Math.Min(preview.Width, item.Bounds.Right) - (int)Math.Ceiling(18 * menu.DeviceDpi / 96.0);
            for (int y = Math.Max(0, item.Bounds.Top + 2); y < Math.Min(preview.Height, item.Bounds.Bottom - 2); y++)
                for (int x = left; x < right; x++)
                {
                    Color pixel = preview.GetPixel(x, y);
                    if (pixel.R >= 180 || pixel.G >= 180 || pixel.B >= 180) continue;
                    pixels++;
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            Check(pixels > 20 && bottom - top >= item.Font.Height / 2,
                "menu text renders visibly: " + item.Text);
            double offset = (top + bottom - item.Bounds.Top - item.Bounds.Bottom + 1) / 2.0;
            Check(Math.Abs(offset) <= 2,
                $"menu glyphs are vertically centered: {item.Text} (offset {offset}px, row {item.Bounds}, ink {top}..{bottom})");
        }
    }

    private static async Task TestNativeWindow()
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--target");
        using Process target = Process.Start(start)!;
        var manager = new WindowManager();
        try
        {
            await Until(() => { target.Refresh(); return target.MainWindowHandle != IntPtr.Zero; }, "external test window starts");
            IntPtr handle = target.MainWindowHandle;
            Check(manager.TrySet(handle, true, IntPtr.Zero, out _) && manager.IsTopMost(handle), "external window pin uses native TOPMOST");
            Check(manager.UnpinAllManaged(out _) == 1 && !manager.IsTopMost(handle), "unpin-all clears every managed external window");
            Check(manager.TrySet(handle, true, IntPtr.Zero, out _) && manager.IsTopMost(handle), "external window can be pinned again after unpin-all");
            using (var hotkey = new HotKeyMessageWindow())
            {
                int presses = 0;
                hotkey.HotKeyPressed += (_, id) => { if (id == 90) presses++; };
                Check(hotkey.TryRegister(90, new HotKeyBinding
                {
                    Modifiers = NativeMethods.HotKeyModifiers.Control | NativeMethods.HotKeyModifiers.Alt | NativeMethods.HotKeyModifiers.Shift,
                    Key = (uint)Keys.F11
                }, out _), "global hotkey registers on native receiver");
                try
                {
                    SendKeys.SendWait("^%+{F11}");
                    await Until(() => presses == 1, "physical key combination delivers WM_HOTKEY");
                }
                finally { hotkey.Unregister(90); }
            }
            using var badge = new PinnedBadgeWindow(handle);
            badge.Sync();
            using var monitor = new WindowLocationMonitor();
            int events = 0;
            monitor.WindowLocationChanged += (_, changed) =>
            {
                if (changed == handle) { events++; badge.QueueSync(); }
            };
            Check(monitor.IsAvailable, "native location event hook registered");
            Point previousBadgeLocation = badge.Location;
            NativeMethods.GetWindowRect(handle, out NativeMethods.Rect previousTarget);
            NativeMethods.SetWindowPos(handle, IntPtr.Zero, 240, 180, 0, 0, NativeMethods.SwpNoSize | 0x0004 | NativeMethods.SwpNoActivate);
            Point expectedLocation = new(previousBadgeLocation.X + 240 - previousTarget.Left,
                previousBadgeLocation.Y + 180 - previousTarget.Top);
            await Until(() => events > 0 && badge.Location == expectedLocation, "badge follows native position event without timer polling");
            NativeMethods.GetWindowRect(handle, out NativeMethods.Rect rect);
            int captionResult = NativeMethods.DwmGetWindowAttribute(handle, NativeMethods.DwmwaCaptionButtonBounds,
                out NativeMethods.Rect buttons, Marshal.SizeOf<NativeMethods.Rect>());
            Check(captionResult == 0 && buttons.Right > buttons.Left &&
                badge.Left >= rect.Left + UiTheme.Scale(8, badge.DeviceDpi) &&
                badge.Right == rect.Left + buttons.Left - UiTheme.Scale(6, badge.DeviceDpi),
                "badge follows the moved target without overlapping its caption buttons");
            ShowWindow(handle, 6);
            await Task.Delay(100);
            badge.Sync();
            Check(!badge.Visible, "minimized target hides badge");
            ShowWindow(handle, 9);
            await Task.Delay(100);
            manager.RestoreManagedWindows();
            Check(!manager.IsTopMost(handle) && manager.Count == 0, "exit restoration unpins managed external window");
        }
        finally
        {
            manager.RestoreManagedWindows();
            if (!target.HasExited)
            {
                target.CloseMainWindow();
                await Until(() => target.HasExited, "external test window closes cleanly");
            }
        }
    }

    private static async Task TestPublished(string path)
    {
        Check(FileVersionInfo.GetVersionInfo(path).FileVersion == "1.0.0.0", "published executable has version 1.0.0");
        using (Icon icon = Icon.ExtractAssociatedIcon(Path.GetFullPath(path))!)
        using (Bitmap bitmap = icon.ToBitmap())
        {
            using Icon application = TrayIconFactory.CreateApplication();
            using var expectedIcon = new Icon(application, bitmap.Size);
            using Bitmap expected = expectedIcon.ToBitmap();
            int mismatched = 0;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).ToArgb() != expected.GetPixel(x, y).ToArgb()) mismatched++;
                }
            Check(mismatched == 0,
                "published EXE native application icon matches the approved embedded v2 icon");
            bitmap.Save(Path.Combine(Artifacts, "application-icon.png"));
        }
        Check(!Process.GetProcessesByName("TopMostTool").Any(), "no existing app instance will be disturbed");
        var start = new ProcessStartInfo(Path.GetFullPath(path))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        };
        string emptyRuntime = Path.Combine(Artifacts, "empty-runtime");
        Directory.CreateDirectory(emptyRuntime);
        start.Environment["DOTNET_ROOT"] = emptyRuntime;
        start.Environment["DOTNET_ROOT_X64"] = emptyRuntime;
        start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        string extractedRuntime = Path.Combine(Artifacts, "bundle-runtime");
        start.Environment["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] = extractedRuntime;
        using Process app = Process.Start(start)!;
        try
        {
            await Until(() => !app.HasExited && Windows(app.Id).Any(window => window.Title == "TopMostToolHotKey"), "published executable initializes native hotkey window");
            Check(!app.HasExited, "published executable starts with runtime lookup pointed at an empty directory");
            Check(Windows(app.Id).Any(window => window.Title == "TopMostToolHotKey"), "published executable created its hidden hotkey receiver");
            app.CloseMainWindow();
            await Task.Delay(250);
            if (!app.HasExited) app.Kill();
            await app.WaitForExitAsync();
            Check(app.HasExited, "published executable can be terminated after startup");
        }
        finally
        {
            if (!app.HasExited)
            {
                // Only the process started by this test is eligible for fallback cleanup.
                app.Kill();
                await app.WaitForExitAsync();
            }
        }
    }

    private static int ForegroundProcessId()
    {
        NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out uint id);
        return (int)id;
    }

    private static List<(IntPtr Handle, string Title)> Windows(int processId)
    {
        var windows = new List<(IntPtr Handle, string Title)>();
        NativeMethods.EnumWindows((window, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(window, out uint id);
            if (id == processId)
            {
                var title = new System.Text.StringBuilder(256);
                NativeMethods.GetWindowText(window, title, title.Capacity);
                windows.Add((window, title.ToString()));
            }
            return true;
        }, IntPtr.Zero);
        IntPtr messageWindow = IntPtr.Zero;
        while ((messageWindow = FindWindowEx(new IntPtr(-3), messageWindow, null, null)) != IntPtr.Zero)
        {
            NativeMethods.GetWindowThreadProcessId(messageWindow, out uint id);
            if (id == processId) windows.Add((messageWindow, "Message-only window"));
        }
        return windows;
    }

    private static async Task Until(Func<bool> condition, string description)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (!condition() && timer.ElapsedMilliseconds < 5000) await Task.Delay(25);
        Check(condition(), description);
    }

    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static object? Invoke(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);
    private static IEnumerable<T> FindControls<T>(Control parent) where T : Control => parent.Controls.Cast<Control>().SelectMany(child => FindControls<T>(child).Concat(child is T match ? new[] { match } : Array.Empty<T>()));
    private static void Mouse(Control control, uint message, int x, int y) => NativeMethods.SendMessage(control.Handle, message, message == 0x0201 ? (IntPtr)1 : IntPtr.Zero, (IntPtr)((y << 16) | (x & 0xffff)));

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void MouseEvent(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? title);
}
