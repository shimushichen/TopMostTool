param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '..\src\TopMostTool\Assets\Source'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\src\TopMostTool\Assets'),
    [switch]$TrayOnly
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class TopMostIconBuilder
{
    public static void Build(string sourcePath, string outputPath, bool tray)
    {
        int[] sizes = { 16, 20, 24, 25, 26, 30, 32, 33, 40, 48, 50, 52, 64, 65, 80, 96, 128, 256 };
        byte[][] frames = new byte[sizes.Length][];
        using (Bitmap source = new Bitmap(sourcePath))
        {
            int left = source.Width, top = source.Height, right = -1, bottom = -1;
            for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                {
                    if (source.GetPixel(x, y).A == 0) continue;
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
            if (right < left) throw new InvalidDataException("The icon source is transparent.");
            Rectangle bounds = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                using (Bitmap frame = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(frame))
                    {
                        g.Clear(Color.Transparent);
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        // Keep the complete antialiased silhouette with clean 1px transparent safety edges.
                        float inset = tray ? Math.Max(1.5f, size * 0.08f) : size * 0.06f;
                        float scale = (size - inset * 2) / Math.Max(bounds.Width, bounds.Height);
                        float width = bounds.Width * scale, height = bounds.Height * scale;
                        g.DrawImage(source, new RectangleF((size - width) / 2 - bounds.Left * scale,
                            (size - height) / 2 - bounds.Top * scale, source.Width * scale, source.Height * scale),
                            new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
                    }
                    // Discard practically invisible bicubic filter tails so outer safety edges stay clean.
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            Color pixel = frame.GetPixel(x, y);
                            if (pixel.A > 0 && pixel.A <= 4)
                                frame.SetPixel(x, y, Color.Transparent);
                        }
                    using (MemoryStream buffer = new MemoryStream())
                    {
                        frame.Save(buffer, ImageFormat.Png);
                        frames[i] = buffer.ToArray();
                    }
                    if (tray)
                        frame.Save(Path.Combine(Path.GetDirectoryName(outputPath), "Tray-" + size + ".png"), ImageFormat.Png);
                }
            }
        }
        using (BinaryWriter writer = new BinaryWriter(File.Create(outputPath)))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(frames[i].Length); writer.Write(offset);
                offset += frames[i].Length;
            }
            foreach (byte[] frame in frames) writer.Write(frame);
        }
        Console.WriteLine(outputPath + " (" + sizes.Length + " sizes)");
    }
}
'@

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (-not $TrayOnly) {
    [TopMostIconBuilder]::Build((Join-Path $SourceDirectory 'application.png'), (Join-Path $OutputDirectory 'Application.ico'), $false)
}
[TopMostIconBuilder]::Build((Join-Path $SourceDirectory 'tray.png'), (Join-Path $OutputDirectory 'Tray.ico'), $true)
