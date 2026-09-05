using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BugRenderer;

/// <summary>
/// Traces a white outline around a rendered bug so dark species stay legible against a dark
/// page. This is a documentation affordance only: the app itself draws no such outline, and
/// nothing here touches the painters.
/// </summary>
public static class OutlineCompositor
{
    /// <summary>
    /// Alpha at or above which a pixel counts as body rather than shadow. The painters lay
    /// their shadow down at alpha 20, so this keeps the outline on the bug instead of halo-ing
    /// the soft blob underneath it.
    /// </summary>
    private const byte SolidAlpha = 96;

    /// <summary>Widens the opaque part of <paramref name="source"/> and fills that ring with white behind it.</summary>
    public static BitmapSource AddOutline(BitmapSource source, int radius)
    {
        var straight = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0.0);
        int width = straight.PixelWidth;
        int height = straight.PixelHeight;
        int stride = width * 4;

        byte[] pixels = new byte[stride * height];
        straight.CopyPixels(pixels, stride, 0);

        byte[] outline = BuildOutlineMask(pixels, width, height, stride, radius);
        Composite(pixels, outline, width, height, stride);

        var result = BitmapSource.Create(
            width, height, straight.DpiX, straight.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// Coverage of the outline ring, as alpha per pixel. Only boundary pixels are grown: the
    /// interior of the shape ends up hidden behind the bug anyway, so splatting a disc from
    /// every solid pixel would do the same work hundreds of times over.
    /// </summary>
    private static byte[] BuildOutlineMask(byte[] pixels, int width, int height, int stride, int radius)
    {
        var disc = BuildDisc(radius);
        byte[] mask = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte alpha = pixels[(y * stride) + (x * 4) + 3];
                if (alpha < SolidAlpha || !IsBoundary(pixels, width, height, stride, x, y))
                {
                    continue;
                }

                foreach (var (dx, dy) in disc)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    {
                        continue;
                    }

                    int index = (ny * width) + nx;
                    if (mask[index] < alpha)
                    {
                        mask[index] = alpha;
                    }
                }
            }
        }

        return mask;
    }

    /// <summary>True when at least one edge-sharing neighbour is not solid, or the pixel is on the image edge.</summary>
    private static bool IsBoundary(byte[] pixels, int width, int height, int stride, int x, int y)
    {
        (int dx, int dy)[] neighbours = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        foreach (var (dx, dy) in neighbours)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
            {
                return true;
            }

            if (pixels[(ny * stride) + (nx * 4) + 3] < SolidAlpha)
            {
                return true;
            }
        }

        return false;
    }

    private static List<(int Dx, int Dy)> BuildDisc(int radius)
    {
        var disc = new List<(int, int)>();
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if ((dx * dx) + (dy * dy) <= radius * radius)
                {
                    disc.Add((dx, dy));
                }
            }
        }

        return disc;
    }

    /// <summary>Paints the bug over the white ring, in place: standard source-over on straight alpha.</summary>
    private static void Composite(byte[] pixels, byte[] outline, int width, int height, int stride)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte ringAlpha = outline[(y * width) + x];
                if (ringAlpha == 0)
                {
                    continue;
                }

                int p = (y * stride) + (x * 4);
                double source = pixels[p + 3] / 255.0;
                double ring = ringAlpha / 255.0;
                double combined = source + (ring * (1.0 - source));
                if (combined <= 0.0)
                {
                    continue;
                }

                // The ring is white, so every channel mixes toward 255 by the same amount.
                for (int channel = 0; channel < 3; channel++)
                {
                    double mixed = ((pixels[p + channel] * source) + (255.0 * ring * (1.0 - source))) / combined;
                    pixels[p + channel] = (byte)Math.Clamp(Math.Round(mixed), 0, 255);
                }

                pixels[p + 3] = (byte)Math.Clamp(Math.Round(combined * 255.0), 0, 255);
            }
        }
    }
}
