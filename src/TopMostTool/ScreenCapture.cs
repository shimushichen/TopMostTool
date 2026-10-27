using System.Drawing.Imaging;

namespace TopMostTool;

internal static class ScreenCapture
{
    internal static Bitmap Capture(Rectangle screenBounds)
    {
        var bitmap = new Bitmap(screenBounds.Width, screenBounds.Height, PixelFormat.Format32bppArgb);
        try
        {
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(screenBounds.Location, Point.Empty, screenBounds.Size,
                CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    internal static Rectangle GetSelection(Point start, Point end, Size canvasSize)
    {
        int left = Math.Clamp(Math.Min(start.X, end.X), 0, canvasSize.Width);
        int top = Math.Clamp(Math.Min(start.Y, end.Y), 0, canvasSize.Height);
        int right = Math.Clamp(Math.Max(start.X, end.X), 0, canvasSize.Width);
        int bottom = Math.Clamp(Math.Max(start.Y, end.Y), 0, canvasSize.Height);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    internal static Bitmap Crop(Bitmap source, Rectangle selection)
    {
        if (selection.Width < 2 || selection.Height < 2 ||
            !new Rectangle(Point.Empty, source.Size).Contains(selection))
        {
            throw new ArgumentOutOfRangeException(nameof(selection));
        }

        return source.Clone(selection, PixelFormat.Format32bppArgb);
    }
}
