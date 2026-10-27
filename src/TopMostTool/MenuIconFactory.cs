using System.Drawing.Drawing2D;

namespace TopMostTool;

internal enum MenuActionIcon
{
    ZoomIn,
    ZoomOut,
    ResetZoom,
    Copy,
    Save,
    Close
}

internal static class MenuIconFactory
{
    private static readonly IReadOnlyDictionary<MenuActionIcon, string> ResourceNames =
        new Dictionary<MenuActionIcon, string>
        {
            [MenuActionIcon.ZoomIn] = "ZoomIn",
            [MenuActionIcon.ZoomOut] = "ZoomOut",
            [MenuActionIcon.ResetZoom] = "ResetZoom",
            [MenuActionIcon.Copy] = "Copy",
            [MenuActionIcon.Save] = "Save",
            [MenuActionIcon.Close] = "Close"
        };

    // Alpha-weighted centroids of the v2-style 256px masters, expressed in
    // pixels at the default 20px render size. Display frames keep that layout.
    private static readonly IReadOnlyDictionary<MenuActionIcon, float> OpticalOffsetY =
        new Dictionary<MenuActionIcon, float>
        {
            [MenuActionIcon.ZoomIn] = 0.358f,
            [MenuActionIcon.ZoomOut] = 0.297f,
            [MenuActionIcon.ResetZoom] = 0.179f,
            [MenuActionIcon.Copy] = -0.048f,
            [MenuActionIcon.Save] = 0.021f,
            [MenuActionIcon.Close] = 0.038f
        };

    internal static Bitmap Create(MenuActionIcon action, int size = 20)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (!ResourceNames.TryGetValue(action, out string? name))
            throw new ArgumentOutOfRangeException(nameof(action));

        string prefix = $"TopMostTool.Assets.ScreenshotMenuIcons.{name}";
        using Stream stream = typeof(MenuIconFactory).Assembly.GetManifestResourceStream($"{prefix}-{size}.png")
            ?? typeof(MenuIconFactory).Assembly.GetManifestResourceStream($"{prefix}.png")
            ?? throw new InvalidOperationException($"The embedded screenshot menu icon is missing: {name}");
        using var source = new Bitmap(stream);
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        // Preserve optical alignment without a fractional translation that
        // interpolates every pixel a second time, including already sharp frames.
        int offsetY = (int)Math.Round(OpticalOffsetY[action] * size / UiTheme.IconSize);
        if (source.Size == bitmap.Size)
            graphics.DrawImageUnscaled(source, 0, offsetY);
        else
            graphics.DrawImage(source, new Rectangle(0, offsetY, size, size),
                0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
        return bitmap;
    }
}
