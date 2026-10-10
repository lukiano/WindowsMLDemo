using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsMlDemo;

public static class DigitPreprocessor
{
    public static float[] FromStrokes(StrokeCollection strokes)
    {
        Rect bounds = strokes.GetBounds();
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("Draw a digit before running inference.");

        // Match MNIST: preserve aspect ratio, fit inside a 20x20 box, white on black.
        const int renderSize = 280;
        double scale = 200 / Math.Max(bounds.Width, bounds.Height);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.Black, null, new Rect(0, 0, renderSize, renderSize));
            var transform = new Matrix(scale, 0, 0, scale,
                (renderSize - bounds.Width * scale) / 2 - bounds.X * scale,
                (renderSize - bounds.Height * scale) / 2 - bounds.Y * scale);
            context.PushTransform(new MatrixTransform(transform));
            strokes.Draw(context);
            context.Pop();
        }
        var bitmap = new RenderTargetBitmap(renderSize, renderSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var bgra = new byte[renderSize * renderSize * 4];
        bitmap.CopyPixels(bgra, renderSize * 4, 0);

        // Area-average each 10x10 block for an antialiased 28x28 tensor.
        var pixels = new float[784];
        for (int y = 0; y < 28; y++)
            for (int x = 0; x < 28; x++)
            {
                float sum = 0;
                for (int dy = 0; dy < 10; dy++)
                    for (int dx = 0; dx < 10; dx++)
                        sum += bgra[((y * 10 + dy) * renderSize + x * 10 + dx) * 4];
                pixels[y * 28 + x] = sum / (100 * 255);
            }

        // MNIST digits are centered by intensity-weighted center of mass.
        double mass = pixels.Sum(p => (double)p);
        if (mass <= 0) throw new ArgumentException("The drawing is empty.");
        double cx = 0, cy = 0;
        for (int i = 0; i < pixels.Length; i++) { cx += (i % 28) * pixels[i]; cy += (i / 28) * pixels[i]; }
        int shiftX = (int)Math.Round(13.5 - cx / mass);
        int shiftY = (int)Math.Round(13.5 - cy / mass);
        var centered = new float[784];
        for (int y = 0; y < 28; y++)
            for (int x = 0; x < 28; x++)
            {
                int sx = x - shiftX, sy = y - shiftY;
                if (sx >= 0 && sx < 28 && sy >= 0 && sy < 28) centered[y * 28 + x] = pixels[sy * 28 + sx];
            }
        return centered;
    }
}
