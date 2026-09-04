using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace IconGen;

/// <summary>
/// Packs bitmaps into a multi-size .ico, which System.Drawing cannot save: Bitmap.Save with
/// ImageFormat.Icon writes a single low-colour image. Each entry carries a PNG payload, which
/// Windows has accepted since Vista.
/// </summary>
public static class IcoWriter
{
    private const int DirectoryEntrySize = 16;
    private const int HeaderSize = 6;

    public static void Write(string path, IReadOnlyList<Bitmap> images)
    {
        var payloads = new List<byte[]>(images.Count);
        foreach (var image in images)
        {
            using var buffer = new MemoryStream();
            image.Save(buffer, ImageFormat.Png);
            payloads.Add(buffer.ToArray());
        }

        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);

        writer.Write((short)0);                 // reserved
        writer.Write((short)1);                 // resource type: icon
        writer.Write((short)images.Count);

        int offset = HeaderSize + (DirectoryEntrySize * images.Count);
        for (int i = 0; i < images.Count; i++)
        {
            // The width and height fields are one byte each, so 256 is stored as 0.
            writer.Write((byte)(images[i].Width == 256 ? 0 : images[i].Width));
            writer.Write((byte)(images[i].Height == 256 ? 0 : images[i].Height));
            writer.Write((byte)0);              // palette size: none, it is a PNG
            writer.Write((byte)0);              // reserved
            writer.Write((short)1);             // colour planes
            writer.Write((short)32);            // bits per pixel
            writer.Write(payloads[i].Length);
            writer.Write(offset);
            offset += payloads[i].Length;
        }

        foreach (byte[] payload in payloads)
        {
            writer.Write(payload);
        }
    }
}
