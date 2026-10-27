using TopMostTool;

internal static partial class Program
{
    private static async Task TestBadgeLifecycle()
    {
        TestBadgeSurfacePixels();
        Rectangle area = Screen.PrimaryScreen!.WorkingArea;
        using var background = new Form
        {
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, TopMost = true,
            StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(area.Left + 100, area.Top + 100, 600, 240),
            BackColor = Color.FromArgb(46, 46, 46)
        };
        background.Show();
        using var badge = new PinnedBadgeWindow(background.Handle);
        badge.Sync();
        await CheckBadgePixels(badge, background.BackColor, "initial");

        badge.Size = new Size(19, 19);
        badge.Sync();
        await CheckBadgePixels(badge, background.BackColor, "after-native-resize");

        badge.Hide();
        badge.Sync();
        await CheckBadgePixels(badge, background.BackColor, "after-show");

        Invoke(badge, "RecreateHandle");
        badge.Sync();
        await CheckBadgePixels(badge, background.BackColor, "after-handle-recreation");

        // Model Terminal's drag-bar child independently of the system DWM
        // metrics, including a title-bar height change on maximization.
        background.BackColor = Color.White;
        using (var dragBar = new BadgeDragBar(background.Handle, background.ClientSize.Width,
                   UiTheme.Scale(40, badge.DeviceDpi)))
        {
            badge.Sync();
            NativeMethods.GetWindowRect(dragBar.Handle, out NativeMethods.Rect bar);
            CheckBadgeCaptionCenter(badge, bar.Top, bar.Bottom, "on a custom title bar");
            await CheckBadgePixels(badge, background.BackColor, "custom-caption");

            background.WindowState = FormWindowState.Maximized;
            await Task.Delay(500);
            dragBar.Resize(background.ClientSize.Width, UiTheme.Scale(32, badge.DeviceDpi));
            badge.Sync();
            NativeMethods.GetWindowRect(dragBar.Handle, out bar);
            CheckBadgeCaptionCenter(badge, bar.Top, bar.Bottom, "on a maximized custom title bar");
            CheckBadgeWithinMonitor(badge, background, "on a maximized custom title bar");
            await CheckBadgePixels(badge, background.BackColor, "custom-caption-maximized");

            background.WindowState = FormWindowState.Normal;
            await Task.Delay(500);
            ShowWindow(dragBar.Handle, 0);
            badge.Sync();
            Check(badge.Top == background.Top + 6, "a hidden drag bar does not shift the badge");
        }
        background.BackColor = Color.FromArgb(46, 46, 46);

        background.FormBorderStyle = FormBorderStyle.Sizable;
        background.ControlBox = true;
        background.MinimizeBox = true;
        background.MaximizeBox = true;
        background.Text = "Badge caption-button verification";
        int darkCaption = 1, captionColor = ColorTranslator.ToWin32(background.BackColor);
        NativeMethods.DwmSetWindowAttribute(background.Handle, 20, ref darkCaption, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(background.Handle, 35, ref captionColor, sizeof(int));
        badge.Sync();
        await CheckBadgePixels(badge, background.BackColor, "on-dark-caption");
        await Task.Delay(100);
        NativeMethods.GetWindowRect(background.Handle, out NativeMethods.Rect window);
        int result = NativeMethods.DwmGetWindowAttribute(background.Handle, NativeMethods.DwmwaCaptionButtonBounds,
            out NativeMethods.Rect buttons, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Rect>());
        Check(result == 0 && buttons.Right > buttons.Left && buttons.Bottom > buttons.Top,
            "test target exposes real DWM caption button bounds");
        int buttonLeft = window.Left + buttons.Left;
        Check(badge.Right <= buttonLeft - UiTheme.Scale(6, badge.DeviceDpi),
            $"badge stays clear of real caption buttons at {badge.DeviceDpi} DPI (badge right {badge.Right}, buttons left {buttonLeft})");
        CheckBadgeCaptionCenter(badge, window.Top + buttons.Top, window.Top + buttons.Bottom, "on a system title bar");
        using Bitmap caption = ScreenCapture.Capture(new Rectangle(
            badge.Left - 4, Math.Min(badge.Top, window.Top), window.Right - badge.Left + 4, 52));
        caption.Save(Path.Combine(Artifacts, "badge-caption.png"));

        background.WindowState = FormWindowState.Maximized;
        // Let the DWM maximize animation finish before sampling desktop pixels.
        await Task.Delay(500);
        badge.Sync();
        SaveVisibleBadgeCaption(badge, background, "badge-caption-maximized.png");
        CheckBadgeWithinMonitor(badge, background, "when the target is maximized");
        NativeMethods.GetWindowRect(background.Handle, out window);
        NativeMethods.DwmGetWindowAttribute(background.Handle, NativeMethods.DwmwaCaptionButtonBounds,
            out buttons, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Rect>());
        NativeMethods.DwmGetWindowAttribute(background.Handle, NativeMethods.DwmwaExtendedFrameBounds,
            out NativeMethods.Rect frame, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Rect>());
        CheckBadgeCaptionCenter(badge, Math.Max(frame.Top, window.Top + buttons.Top), window.Top + buttons.Bottom,
            "on a maximized system title bar");
        await CheckBadgePixels(badge, background.BackColor, "maximized");

        background.WindowState = FormWindowState.Normal;
        background.FormBorderStyle = FormBorderStyle.None;
        background.Bounds = new Rectangle(area.Left + 100, Screen.FromHandle(background.Handle).Bounds.Top - 40, 600, 240);
        await Task.Delay(500);
        badge.Sync();
        SaveVisibleBadgeCaption(badge, background, "badge-caption-partially-offscreen.png");
        CheckBadgeWithinMonitor(badge, background, "when the target extends above the screen");
        await CheckBadgePixels(badge, background.BackColor, "partially-offscreen");
    }

    private static void TestBadgeSurfacePixels()
    {
        using Bitmap image = TrayIconFactory.CreatePinImage(26);
        foreach (float sourceDpi in new[] { image.HorizontalResolution, 72F, 96F, 120F, 144F, 192F })
        {
            image.SetResolution(sourceDpi, sourceDpi);
            using Bitmap surface = PinnedBadgeWindow.CreateLayeredSurface(image);
            int mismatched = 0, alphaMismatched = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    Color expected = CompositePinPixel(image.GetPixel(x, y), Color.FromArgb(46, 46, 46));
                    Color actual = CompositePinPixel(surface.GetPixel(x, y), Color.FromArgb(46, 46, 46));
                    if (Math.Abs(expected.R - actual.R) > 2 || Math.Abs(expected.G - actual.G) > 2 ||
                        Math.Abs(expected.B - actual.B) > 2) mismatched++;
                    if (image.GetPixel(x, y).A != surface.GetPixel(x, y).A) alphaMismatched++;
                }
            surface.Save(Path.Combine(Artifacts, $"badge-surface-source-{sourceDpi}dpi.png"));
            Check(mismatched == 0 && alphaMismatched == 0,
                $"layered surface copies raw PNG pixels at {sourceDpi} source DPI and {surface.HorizontalResolution} surface DPI ({mismatched} color, {alphaMismatched} alpha differences)");
        }
    }

    private static Color CompositePinPixel(Color pixel, Color background)
    {
        static int Blend(byte source, byte alpha, byte background)
            => (source * alpha + background * (255 - alpha) + 127) / 255;
        return Color.FromArgb(Blend(pixel.R, pixel.A, background.R),
            Blend(pixel.G, pixel.A, background.G), Blend(pixel.B, pixel.A, background.B));
    }

    private static void CheckBadgeCaptionCenter(PinnedBadgeWindow badge, int top, int bottom, string state)
    {
        using Bitmap image = TrayIconFactory.CreatePinImage(26);
        double alpha = 0, moment = 0;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                byte opacity = image.GetPixel(x, y).A;
                alpha += opacity;
                moment += (badge.Top + y + 0.5) * opacity;
            }
        double offset = moment / alpha - (top + bottom) / 2.0;
        Check(Math.Abs(offset) <= 1 && badge.Top >= top && badge.Bottom <= bottom,
            $"the complete pin is optically centered {state} (offset {offset:F2}px; badge {badge.Top}..{badge.Bottom}, caption {top}..{bottom})");
    }

    private sealed class BadgeDragBar : IDisposable
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential,
            CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private struct WindowClass
        {
            public uint Style;
            public WindowProcedure Procedure;
            public int ClassExtra, WindowExtra;
            public IntPtr Instance, Icon, Cursor, Background;
            public string? MenuName;
            public string ClassName;
        }

        private delegate IntPtr WindowProcedure(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
        private static readonly WindowProcedure Procedure = DefWindowProc;
        private static readonly IntPtr Instance = GetModuleHandle(null);
        private static readonly ushort Atom = Register();
        internal IntPtr Handle { get; }

        internal BadgeDragBar(IntPtr parent, int width, int height)
        {
            if (Atom == 0) throw new System.ComponentModel.Win32Exception();
            Handle = CreateWindowEx(0, "DRAG_BAR_WINDOW_CLASS", null, 0x50000000,
                0, 1, width, height, parent, IntPtr.Zero, Instance, IntPtr.Zero);
            if (Handle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        }

        private static ushort Register()
        {
            var windowClass = new WindowClass
            {
                Procedure = Procedure, Instance = Instance,
                Background = new IntPtr(6), ClassName = "DRAG_BAR_WINDOW_CLASS"
            };
            return RegisterClass(ref windowClass);
        }

        internal void Resize(int width, int height) => NativeMethods.SetWindowPos(
            Handle, IntPtr.Zero, 0, 1, width, height, 0x0004 | NativeMethods.SwpNoActivate);
        public void Dispose() => DestroyWindow(Handle);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern ushort RegisterClass(ref WindowClass windowClass);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? name);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(uint exStyle, string className, string? title, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr handle);
    }

    private static void CheckBadgeWithinMonitor(PinnedBadgeWindow badge, Form target, string state)
    {
        Rectangle screen = Screen.FromHandle(target.Handle).Bounds;
        Check(screen.Contains(badge.Bounds),
            $"the complete badge canvas stays on screen {state} (badge {badge.Bounds}, monitor {screen})");
    }

    private static void SaveVisibleBadgeCaption(PinnedBadgeWindow badge, Form target, string fileName)
    {
        Rectangle screen = Screen.FromHandle(target.Handle).Bounds;
        Rectangle crop = Rectangle.Intersect(screen, new Rectangle(badge.Left - 4, badge.Top - 4, 230, 52));
        using Bitmap caption = ScreenCapture.Capture(crop);
        caption.Save(Path.Combine(Artifacts, fileName));
    }

    private static async Task CheckBadgePixels(PinnedBadgeWindow badge, Color background, string state)
    {
        await Task.Delay(120);
        using Bitmap image = TrayIconFactory.CreatePinImage(26);
        NativeMethods.GetWindowRect(badge.Handle, out NativeMethods.Rect native);
        Check(badge.Size == image.Size && badge.ClientSize == image.Size &&
            native.Right - native.Left == image.Width && native.Bottom - native.Top == image.Height,
            $"badge window, client and alpha surface keep the same pixel size {state}");
        Check(badge.Location == new Point(native.Left, native.Top),
            $"managed and native badge positions agree {state} (managed {badge.Location}, native {native.Left},{native.Top})");
        using Bitmap actual = ScreenCapture.Capture(badge.Bounds);
        int mismatched = 0;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                Color a = actual.GetPixel(x, y), b = CompositePinPixel(image.GetPixel(x, y), background);
                if (Math.Abs(a.R - b.R) > 4 || Math.Abs(a.G - b.G) > 4 || Math.Abs(a.B - b.B) > 4) mismatched++;
            }
        actual.Save(Path.Combine(Artifacts, $"badge-{state}.png"));
        Check(mismatched == 0, $"badge keeps the complete source silhouette {state} ({mismatched} differing pixels)");
    }
}
