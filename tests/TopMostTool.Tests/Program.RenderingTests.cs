using TopMostTool;

internal static partial class Program
{
    private static async Task TestRendering()
    {
        TestPinSilhouette();
        TestMenuPinReadability();
        await TestTrayPinImageSlot();
        foreach (int dpi in new[] { 96, 120, 144, 192 })
        {
            int size = UiTheme.Scale(UiTheme.IconSize, dpi);
            using Bitmap pin = TrayIconFactory.CreatePinImage(size);
            Check(pin.Size == new Size(size, size), $"pin supports the exact {dpi} DPI menu size without enlarging a small frame");
            using var menu = new ContextMenuStrip();
            UiTheme.StyleMenu(menu);
            foreach (MenuActionIcon action in Enum.GetValues<MenuActionIcon>())
            {
                using Bitmap icon = MenuIconFactory.Create(action, size);
                using var item = new ToolStripMenuItem { Image = icon };
                UiTheme.StyleMenuItem(item);
                using var actual = new Bitmap(size + 10, size + 10);
                using var expected = new Bitmap(size + 10, size + 10);
                using (Graphics graphics = Graphics.FromImage(expected))
                {
                    graphics.Clear(UiTheme.Surface);
                    graphics.DrawImageUnscaled(icon, 5, 5);
                }
                using (Graphics graphics = Graphics.FromImage(actual))
                {
                    graphics.Clear(UiTheme.Surface);
                    menu.Renderer.DrawItemImage(new ToolStripItemImageRenderEventArgs(graphics, item,
                        icon, new Rectangle(5, 5, size, size)));
                }
                Check(SamePixels(actual, expected), $"{action} menu image preserves exact pixels at {dpi} DPI");
            }
        }
        using (var about = new AboutForm())
        {
            about.Show();
            await Task.Delay(40);
            PictureBox picture = FindControls<PictureBox>(about).Single();
            Check(picture.Image!.Size == picture.ClientSize && picture.SizeMode == PictureBoxSizeMode.CenterImage,
                "About uses an image at the physical display size instead of stretching a small ICO frame");
            SaveControlPreview(about, "about-sharp.png");
            about.Close();
        }
        using (var image = new PinnedImageWindow(CreatePattern(), new Point(180, 180), 1))
        {
            image.Show();
            await Task.Delay(40);
            int size = UiTheme.Scale(UiTheme.IconSize, image.DeviceDpi);
            Check(FindControls<Button>(image).All(button => button is RoundedButton && button.Region is null),
                "every toolbar button uses one rounded painter without a clipping region");
            Check(FindControls<Button>(image).Where(button => button.Image is not null).All(button => button.Image!.Size == new Size(size, size)),
                "toolbar icons match the monitor's physical pixel size");
            image.ContextMenuStrip!.Show(image, new Point(30, 40));
            await Task.Delay(40);
            ContextMenuStrip menu = image.ContextMenuStrip;
            Check(menu.Items.OfType<ToolStripMenuItem>().All(item => item.Image?.Size == menu.ImageScalingSize &&
                item.ImageScaling == ToolStripItemImageScaling.None), "screenshot menu renders final-size bitmaps without ToolStrip resampling");
            menu.Close();
            SaveControlPreview(image, "pinned-image-sharp.png");
            image.Close();
        }
        await TestButtonBorders();
    }

    private static void TestMenuPinReadability()
    {
        for (int size = 12; size <= 64; size++)
        {
            using Bitmap pin = TrayIconFactory.CreateMenuPinImage(size);
            int teal = 0;
            bool clearEdges = true;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color pixel = pin.GetPixel(x, y);
                    if (x == 0 || y == 0 || x == size - 1 || y == size - 1)
                        clearEdges &= pixel.A == 0;
                    if (pixel.A < 160) continue;
                    if (pixel.G > pixel.R + 40 && pixel.B > 80) teal++;
                }
            Check(pin.Size == new Size(size, size) && clearEdges && teal > size * size / 16,
                $"{size}px menu pin preserves the teal artwork and transparent safety edges");
        }
        foreach (int size in new[] { 16, 20, 24, 32 })
        {
            using Bitmap approved = TrayIconFactory.CreatePinImage(size);
            using Bitmap menu = TrayIconFactory.CreateMenuPinImage(size);
            (int dark, int teal) reference = CountPinColors(approved);
            (int dark, int teal) actual = CountPinColors(menu);
            // Different sampling filters can shift a few edge pixels. A new
            // solid dark rim, however, must not replace the approved shading.
            Check(actual.dark <= reference.dark + Math.Max(3, size / 3) &&
                Math.Abs(actual.teal - reference.teal) <= Math.Max(4, reference.teal / 8),
                $"{size}px menu pin retains the approved v2 color balance without an extra dark rim");
        }
    }

    private static (int dark, int teal) CountPinColors(Bitmap bitmap)
    {
        int dark = 0, teal = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.A < 160) continue;
                if (pixel.R < 70 && pixel.G < 100 && pixel.B < 110) dark++;
                if (pixel.G > pixel.R + 40 && pixel.B > 80) teal++;
            }
        return (dark, teal);
    }

    private static async Task TestTrayPinImageSlot()
    {
        using var menu = new ContextMenuStrip();
        UiTheme.StyleMenu(menu);
        using var frame = (Bitmap)typeof(TrayApplicationContext)
            .GetMethod("CreatePinMenuImage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, new object[] { menu.DeviceDpi })!;
        var item = new ToolStripMenuItem("截图贴图") { Image = frame };
        UiTheme.StyleMenuItem(item);
        menu.Items.Add(item);
        ToolStripRenderer renderer = menu.Renderer;
        var probe = new MenuImageSlotProbe(renderer);
        menu.Renderer = probe;
        menu.Show(100, 100);
        try
        {
            await Task.Delay(40);
            using var preview = new Bitmap(menu.Width, menu.Height);
            menu.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
            Check(probe.ImageSlot.Size == frame.Size,
                $"native tray-menu image slot uses the final bitmap size at {menu.DeviceDpi} DPI ({probe.ImageSlot.Size})");
        }
        finally { menu.Close(); }
    }

    private sealed class MenuImageSlotProbe(ToolStripRenderer renderer) : ToolStripProfessionalRenderer
    {
        internal Rectangle ImageSlot { get; private set; }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            ImageSlot = e.ImageRectangle;
            renderer.DrawItemImage(e);
        }
    }

    private static void TestPinSilhouette()
    {
        foreach (int size in new[] { 16, 20, 24, 25, 26, 30, 32, 33, 40, 48, 50, 52, 64, 65, 80, 96, 128, 256 })
        {
            using Bitmap pin = TrayIconFactory.CreatePinImage(size);
            bool clearEdge = true;
            for (int pixel = 0; pixel < size; pixel++)
                clearEdge &= pin.GetPixel(pixel, 0).A == 0 && pin.GetPixel(pixel, size - 1).A == 0 &&
                    pin.GetPixel(0, pixel).A == 0 && pin.GetPixel(size - 1, pixel).A == 0;
            Check(clearEdge, $"{size}px pin keeps its complete silhouette inside transparent outer edges");
        }
        using (Icon tray = TrayIconFactory.Create())
        using (Bitmap native = tray.ToBitmap())
        using (Bitmap png = TrayIconFactory.CreatePinImage(tray.Width))
        {
            native.Save(Path.Combine(Artifacts, "pin-native.png"));
            png.Save(Path.Combine(Artifacts, "pin-menu.png"));
            bool sameSilhouette = true, sameVisibleColors = true;
            for (int y = 0; y < png.Height; y++)
                for (int x = 0; x < png.Width; x++)
                    sameSilhouette &= native.GetPixel(x, y).A == png.GetPixel(x, y).A;
            // ICO.ToBitmap and Bitmap cloning round premultiplied RGB values
            // differently at very low alpha. Compare visible composites too.
            foreach (Color background in new[] { Color.White, Color.FromArgb(32, 32, 32) })
            {
                using var actual = new Bitmap(png.Width, png.Height);
                using var expected = new Bitmap(png.Width, png.Height);
                using (Graphics graphics = Graphics.FromImage(actual))
                { graphics.Clear(background); graphics.DrawImageUnscaled(native, Point.Empty); }
                using (Graphics graphics = Graphics.FromImage(expected))
                { graphics.Clear(background); graphics.DrawImageUnscaled(png, Point.Empty); }
                for (int y = 0; y < png.Height; y++)
                    for (int x = 0; x < png.Width; x++)
                    {
                        Color a = actual.GetPixel(x, y), b = expected.GetPixel(x, y);
                        sameVisibleColors &= Math.Abs(a.R - b.R) <= 2 && Math.Abs(a.G - b.G) <= 2 && Math.Abs(a.B - b.B) <= 2;
                    }
            }
            Check(sameSilhouette, "title bar/tray ICO preserves the exact complete alpha silhouette of the menu PNG");
            Check(sameVisibleColors, "native and menu pin frames match on light and dark surfaces within alpha rounding tolerance");
        }

        using var menu = new ContextMenuStrip();
        UiTheme.StyleMenu(menu);
        using Bitmap largePin = TrayIconFactory.CreatePinImage(32);
        foreach (bool enabled in new[] { true, false })
        {
            using var item = new ToolStripMenuItem { Image = largePin, Enabled = enabled };
            using var canvas = new Bitmap(40, 40);
            Rectangle slot = new(8, 8, 16, 16);
            using (Graphics graphics = Graphics.FromImage(canvas))
            {
                graphics.Clear(UiTheme.Surface);
                menu.Renderer.DrawItemImage(new ToolStripItemImageRenderEventArgs(graphics, item, largePin, slot));
            }
            bool fits = true;
            for (int y = 0; y < canvas.Height; y++)
                for (int x = 0; x < canvas.Width; x++)
                    if (!slot.Contains(x, y) && canvas.GetPixel(x, y).ToArgb() != UiTheme.Surface.ToArgb()) fits = false;
            Check(fits, $"{(enabled ? "enabled" : "disabled")} menu pin fits a smaller native image slot without clipping or overflowing");
        }
    }

    private static async Task TestButtonBorders()
    {
        using var form = new Form { BackColor = Color.FromArgb(235, 238, 241), ClientSize = new Size(660, 230) };
        var stateNames = new[] { "normal", "hover", "pressed", "disabled", "keyboard focus", "primary", "danger" };
        for (int index = 0; index < stateNames.Length; index++)
        {
            var button = new RoundedButton { Text = stateNames[index], TabIndex = index };
            UiTheme.StyleButton(button, primary: index == 5, danger: index == 6);
            button.AutoSize = false;
            button.SetBounds(20 + index % 4 * 160, 25 + index / 4 * 90, 140, 45);
            form.Controls.Add(button);
        }
        form.Show();
        await Task.Delay(40);
        RoundedButton[] buttons = form.Controls.OfType<RoundedButton>().ToArray();
        buttons[0].Focus();
        Cursor.Position = form.PointToScreen(new Point(form.ClientSize.Width - 5, form.ClientSize.Height - 5));
        Invoke(buttons[1], "OnMouseEnter", EventArgs.Empty);
        Invoke(buttons[2], "OnMouseEnter", EventArgs.Empty);
        Invoke(buttons[2], "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 20, 20, 0));
        buttons[3].Enabled = false;
        buttons[4].Focus();
        NativeMethods.SendMessage(buttons[4].Handle, 0x0127, (IntPtr)0x00010002, IntPtr.Zero); // clear UISF_HIDEFOCUS
        Invoke(buttons[6], "OnMouseEnter", EventArgs.Empty);
        foreach (RoundedButton button in buttons)
        {
            using var bitmap = new Bitmap(button.Width, button.Height);
            button.DrawToBitmap(bitmap, button.ClientRectangle);
            Check(bitmap.GetPixel(0, 0).ToArgb() == form.BackColor.ToArgb(), $"{button.Text} button corners retain the parent surface");
            if (button.Text != "primary")
            {
                // The previous Region-clipped native border lost all four arcs.
                // Require a real curved stroke, plus connected straight edges.
                int darkArc = 0;
                for (int y = 1; y < UiTheme.Scale(8, button.DeviceDpi); y++)
                    for (int x = 1; x < UiTheme.Scale(8, button.DeviceDpi); x++)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        if (pixel.R < 227 && pixel.G < 233 && pixel.B < 237) darkArc++;
                    }
                Check(darkArc > 0, $"{button.Text} button draws its rounded border arc");
            }
        }
        SaveControlPreview(form, "rounded-button-states.png");
        form.Close();
    }

    private static bool SamePixels(Bitmap first, Bitmap second)
    {
        if (first.Size != second.Size) return false;
        for (int y = 0; y < first.Height; y++)
            for (int x = 0; x < first.Width; x++)
                if (first.GetPixel(x, y).ToArgb() != second.GetPixel(x, y).ToArgb()) return false;
        return true;
    }
}
