using System.Drawing.Drawing2D;

namespace TopMostTool;

internal static class TrayIconFactory
{
    internal static Icon Create()
    {
        return LoadIcon(SystemInformation.SmallIconSize);
    }

    internal static Icon CreateApplication()
    {
        using Stream stream = typeof(TrayIconFactory).Assembly.GetManifestResourceStream("TopMostTool.Assets.Application.ico")
            ?? throw new InvalidOperationException("The embedded application icon is missing.");
        return new Icon(stream);
    }

    internal static Bitmap CreatePinImage(int size = 16)
        => LoadPinImage($"TopMostTool.Assets.Tray-{size}.png", size);

    internal static Bitmap CreateMenuPinImage(int size)
        => LoadPinImage($"TopMostTool.Assets.MenuPinIcons.Pin-{size}.png", size);

    private static Bitmap LoadPinImage(string resourceName, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        using Stream? stream = typeof(TrayIconFactory).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return CreateSourceImage("tray", size);
        using var image = new Bitmap(stream);
        // Clone the pixels directly; new Bitmap(image) draws through GDI+ and
        // rounds the RGB values of translucent edge pixels a second time.
        return image.Clone(new Rectangle(Point.Empty, image.Size), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    }

    internal static Bitmap CreateApplicationImage(int size) => CreateSourceImage("application", size);

    private static Bitmap CreateSourceImage(string name, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        using Stream stream = typeof(TrayIconFactory).Assembly.GetManifestResourceStream($"TopMostTool.Assets.Source.{name}.png")
            ?? throw new InvalidOperationException("The embedded high-resolution icon source is missing.");
        using var source = new Bitmap(stream);
        int left = source.Width, top = source.Height, right = -1, bottom = -1;
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                if (source.GetPixel(x, y).A == 0) continue;
                left = Math.Min(left, x); top = Math.Min(top, y);
                right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
        if (right < left) throw new InvalidOperationException("The embedded icon source is transparent.");
        Rectangle bounds = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        float inset = name == "tray" ? Math.Max(2F, size * 0.10F) : size * 0.08F;
        float scale = Math.Max(1, size - inset * 2) / Math.Max(bounds.Width, bounds.Height);
        float width = bounds.Width * scale, height = bounds.Height * scale;
        graphics.DrawImage(source, new RectangleF((size - width) / 2 - bounds.Left * scale,
            (size - height) / 2 - bounds.Top * scale, source.Width * scale, source.Height * scale),
            new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
        return bitmap;
    }

    private static Icon LoadIcon(Size size)
    {
        using Stream stream = typeof(TrayIconFactory).Assembly.GetManifestResourceStream("TopMostTool.Assets.Tray.ico")
            ?? throw new InvalidOperationException("The embedded tray icon is missing.");
        return new Icon(stream, size);
    }
}

internal static class GraphicsExtensions
{
    internal static void FillRoundedRectangle(this Graphics graphics, Brush brush, float x, float y, float width, float height, float radius)
    {
        using GraphicsPath path = RoundedRectanglePath(x, y, width, height, radius);
        graphics.FillPath(brush, path);
    }

    internal static void DrawRoundedRectangle(this Graphics graphics, Pen pen, float x, float y, float width, float height, float radius)
    {
        using GraphicsPath path = RoundedRectanglePath(x, y, width, height, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRectanglePath(float x, float y, float width, float height, float radius)
    {
        radius = Math.Max(0.5F, Math.Min(radius, Math.Min(width, height) / 2));
        float diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
