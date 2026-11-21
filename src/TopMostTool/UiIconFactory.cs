using System.Drawing.Drawing2D;

namespace TopMostTool;

internal enum UiIcon
{
    Screenshot,
    SelectWindow,
    CloseScreenshots,
    UnpinWindow,
    UnpinAll,
    Settings,
    Startup,
    About,
    Exit,
    Error,
    Copy,
    Save,
    Close,
    ZoomIn,
    ZoomOut,
    ResetZoom
}

// SF-Symbols-inspired minimalist monochrome vector icons with uniform crisp strokes.
internal static class UiIconFactory
{
    internal static Bitmap Create(UiIcon icon, int size, Color? colorOverride = null)
    {
        var bitmap = new Bitmap(size, size);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float scale = size / 24F;
        graphics.ScaleTransform(scale, scale);

        Color ink = SystemInformation.HighContrast ? SystemColors.WindowText
            : colorOverride ?? (icon is UiIcon.Error ? UiTheme.Error : Color.FromArgb(44, 44, 46));

        using var pen = new Pen(ink, 1.55F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var brush = new SolidBrush(ink);

        switch (icon)
        {
            case UiIcon.Screenshot:
                graphics.DrawLines(pen, new[] { new PointF(8.5F, 4.5F), new PointF(4.5F, 4.5F), new PointF(4.5F, 8.5F) });
                graphics.DrawLines(pen, new[] { new PointF(15.5F, 4.5F), new PointF(19.5F, 4.5F), new PointF(19.5F, 8.5F) });
                graphics.DrawLines(pen, new[] { new PointF(4.5F, 15.5F), new PointF(4.5F, 19.5F), new PointF(8.5F, 19.5F) });
                graphics.DrawLines(pen, new[] { new PointF(15.5F, 19.5F), new PointF(19.5F, 19.5F), new PointF(19.5F, 15.5F) });
                graphics.FillEllipse(brush, 10.6F, 10.6F, 2.8F, 2.8F);
                break;

            case UiIcon.SelectWindow:
                graphics.DrawRoundedRectangle(pen, 3.8F, 4.2F, 16.4F, 14.6F, 2.5F);
                graphics.DrawLine(pen, 3.8F, 8.2F, 20.2F, 8.2F);
                graphics.FillEllipse(brush, 6.2F, 5.6F, 1.3F, 1.3F);
                graphics.FillEllipse(brush, 8.6F, 5.6F, 1.3F, 1.3F);
                var arrow = new PointF[]
                {
                    new(12.2F, 11.2F), new(18.2F, 16.9F), new(15.1F, 16.7F),
                    new(13.9F, 19.3F), new(12.1F, 18.4F), new(13.2F, 15.9F), new(10.8F, 15.4F)
                };
                graphics.FillPolygon(brush, arrow);
                break;

            case UiIcon.CloseScreenshots:
                graphics.DrawLines(pen, new[] { new PointF(7.2F, 4.2F), new PointF(19.2F, 4.2F), new PointF(19.2F, 13.2F) });
                graphics.DrawRoundedRectangle(pen, 3.8F, 7.2F, 12.2F, 11.6F, 2.2F);
                graphics.DrawLine(pen, 3.8F, 10.6F, 16F, 10.6F);
                graphics.DrawLine(pen, 15.6F, 15.6F, 19.6F, 19.6F);
                graphics.DrawLine(pen, 19.6F, 15.6F, 15.6F, 19.6F);
                break;

            case UiIcon.UnpinWindow:
                graphics.DrawRoundedRectangle(pen, 3.8F, 4.6F, 16.4F, 14.6F, 2.5F);
                graphics.DrawLine(pen, 3.8F, 8.8F, 20.2F, 8.8F);
                graphics.DrawLine(pen, 8.8F, 14F, 15.2F, 14F);
                break;

            case UiIcon.UnpinAll:
                graphics.DrawLines(pen, new[] { new PointF(7.2F, 4.2F), new PointF(19.2F, 4.2F), new PointF(19.2F, 13F) });
                graphics.DrawRoundedRectangle(pen, 3.8F, 7.2F, 12.2F, 11.6F, 2.2F);
                graphics.DrawLine(pen, 3.8F, 10.6F, 16F, 10.6F);
                graphics.DrawLine(pen, 15F, 16.8F, 19.8F, 16.8F);
                break;

            case UiIcon.Settings:
                graphics.DrawLine(pen, 4.2F, 6.8F, 19.8F, 6.8F);
                graphics.DrawLine(pen, 4.2F, 12F, 19.8F, 12F);
                graphics.DrawLine(pen, 4.2F, 17.2F, 19.8F, 17.2F);
                DrawKnob(graphics, pen, 8.8F, 6.8F);
                DrawKnob(graphics, pen, 15.4F, 12F);
                DrawKnob(graphics, pen, 10.2F, 17.2F);
                break;

            case UiIcon.Startup:
                graphics.DrawLine(pen, 12F, 15.2F, 12F, 5.2F);
                graphics.DrawLines(pen, new[]
                {
                    new PointF(8.2F, 8.8F),
                    new PointF(12F, 5F),
                    new PointF(15.8F, 8.8F)
                });
                graphics.DrawLines(pen, new[]
                {
                    new PointF(5.5F, 16.2F),
                    new PointF(5.5F, 18.8F),
                    new PointF(18.5F, 18.8F),
                    new PointF(18.5F, 16.2F)
                });
                break;

            case UiIcon.About:
                graphics.DrawEllipse(pen, 4.2F, 4.2F, 15.6F, 15.6F);
                graphics.FillEllipse(brush, 11.1F, 7.6F, 1.8F, 1.8F);
                graphics.DrawLine(pen, 12F, 11.2F, 12F, 16F);
                break;

            case UiIcon.Exit:
                graphics.DrawArc(pen, 4.8F, 4.8F, 14.4F, 14.4F, -56, 292);
                graphics.DrawLine(pen, 12F, 3.2F, 12F, 10.2F);
                break;

            case UiIcon.Error:
                graphics.DrawEllipse(pen, 4.2F, 4.2F, 15.6F, 15.6F);
                graphics.DrawLine(pen, 12F, 7.6F, 12F, 12.6F);
                graphics.FillEllipse(brush, 11.1F, 14.9F, 1.8F, 1.8F);
                break;

            case UiIcon.Copy:
                // SF-Symbols style overlapping rounded sheets
                graphics.DrawRoundedRectangle(pen, 8.5F, 8.2F, 10.5F, 11.3F, 2.2F);
                graphics.DrawLines(pen, new[]
                {
                    new PointF(5.5F, 15.2F),
                    new PointF(5.5F, 6.2F),
                    new PointF(7.2F, 4.5F),
                    new PointF(15.2F, 4.5F)
                });
                break;

            case UiIcon.Save:
                // SF-Symbols style download-to-tray save arrow
                graphics.DrawLine(pen, 12F, 4.8F, 12F, 14.6F);
                graphics.DrawLines(pen, new[]
                {
                    new PointF(8.3F, 11.1F),
                    new PointF(12F, 14.8F),
                    new PointF(15.7F, 11.1F)
                });
                graphics.DrawLines(pen, new[]
                {
                    new PointF(5.2F, 16F),
                    new PointF(5.2F, 18.8F),
                    new PointF(18.8F, 18.8F),
                    new PointF(18.8F, 16F)
                });
                break;

            case UiIcon.Close:
                // Minimalist crisp × close symbol
                graphics.DrawLine(pen, 7.2F, 7.2F, 16.8F, 16.8F);
                graphics.DrawLine(pen, 16.8F, 7.2F, 7.2F, 16.8F);
                break;

            case UiIcon.ZoomIn:
                graphics.DrawEllipse(pen, 4.5F, 4.5F, 11.8F, 11.8F);
                graphics.DrawLine(pen, 14.8F, 14.8F, 19.2F, 19.2F);
                graphics.DrawLine(pen, 10.4F, 7.8F, 10.4F, 13F);
                graphics.DrawLine(pen, 7.8F, 10.4F, 13F, 10.4F);
                break;

            case UiIcon.ZoomOut:
                graphics.DrawEllipse(pen, 4.5F, 4.5F, 11.8F, 11.8F);
                graphics.DrawLine(pen, 14.8F, 14.8F, 19.2F, 19.2F);
                graphics.DrawLine(pen, 7.8F, 10.4F, 13F, 10.4F);
                break;

            case UiIcon.ResetZoom:
                // Clean circular reset arrow
                graphics.DrawArc(pen, 5.2F, 5.2F, 13.6F, 13.6F, -55, 290);
                graphics.DrawLines(pen, new[]
                {
                    new PointF(14.8F, 4.8F),
                    new PointF(17.8F, 7.2F),
                    new PointF(14.5F, 9.2F)
                });
                break;
        }
        return bitmap;
    }

    private static void DrawKnob(Graphics graphics, Pen pen, float x, float y)
    {
        using var white = new SolidBrush(Color.White);
        graphics.FillEllipse(white, x - 2.1F, y - 2.1F, 4.2F, 4.2F);
        graphics.DrawEllipse(pen, x - 2.1F, y - 2.1F, 4.2F, 4.2F);
    }
}
