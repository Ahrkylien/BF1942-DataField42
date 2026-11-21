using SkiaSharp;
using Pfim;

namespace DataField42.Website.Services.Imaging;

/// <summary>
/// Converts the game's own image formats (DDS and TGA, plus the ordinary formats mods ship
/// screenshots in) into something a browser can display. Images with transparency become PNG,
/// everything else becomes a smaller JPEG.
/// </summary>
public static class GameImageConverter
{
    private const int JpegQuality = 90;

    /// <summary>
    /// Converts <paramref name="sourcePath"/> and writes it next to
    /// <paramref name="destinationPathWithoutExtension"/> with the extension the chosen format
    /// implies. Returns the path written, or <see langword="null"/> if the image could not be
    /// decoded.
    /// </summary>
    public static string? Convert(string sourcePath, string destinationPathWithoutExtension)
    {
        using var bitmap = TryDecode(sourcePath);
        if (bitmap is null)
            return null;

        bool hasTransparency = HasTransparency(bitmap);
        string destination = destinationPathWithoutExtension + (hasTransparency ? ".png" : ".jpg");
        string? directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = hasTransparency
            ? image.Encode(SKEncodedImageFormat.Png, 100)
            : image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

        using var output = File.Create(destination);
        encoded.SaveTo(output);
        return destination;
    }

    private static SKBitmap? TryDecode(string path)
    {
        try
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".dds" or ".tga")
                return DecodeWithPfim(path);

            return SKBitmap.Decode(path);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// DDS and TGA are decoded by Pfim, which hands back raw pixels in the file's own layout;
    /// they are copied into an <see cref="SKBitmap"/> row by row, converting BGR(A) to RGBA and
    /// the packed 16-bit layouts to 8 bits per channel.
    /// </summary>
    private static SKBitmap? DecodeWithPfim(string path)
    {
        using var decoded = Pfimage.FromFile(path);
        if (decoded.Compressed)
            decoded.Decompress();

        var bitmap = new SKBitmap(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bool filled = decoded.Format switch
        {
            ImageFormat.Rgba32 => FillFromBgra32(bitmap, decoded),
            ImageFormat.Rgb24 => FillFromBgr24(bitmap, decoded),
            ImageFormat.Rgba16 => FillFromRgba16(bitmap, decoded),
            ImageFormat.R5g5b5 or ImageFormat.R5g5b5a1 or ImageFormat.R5g6b5 => FillFromR5G6B5(bitmap, decoded),
            ImageFormat.Rgb8 => FillFromGray8(bitmap, decoded),
            _ => false,
        };

        if (filled)
            return bitmap;

        bitmap.Dispose();
        return null;
    }

    private static bool FillFromBgra32(SKBitmap bitmap, IImage decoded)
    {
        var data = decoded.Data;
        for (int y = 0; y < decoded.Height; y++)
        {
            int row = y * decoded.Stride;
            for (int x = 0; x < decoded.Width; x++)
            {
                int i = row + x * 4;
                bitmap.SetPixel(x, y, new SKColor(data[i + 2], data[i + 1], data[i], data[i + 3]));
            }
        }
        return true;
    }

    private static bool FillFromBgr24(SKBitmap bitmap, IImage decoded)
    {
        var data = decoded.Data;
        for (int y = 0; y < decoded.Height; y++)
        {
            int row = y * decoded.Stride;
            for (int x = 0; x < decoded.Width; x++)
            {
                int i = row + x * 3;
                bitmap.SetPixel(x, y, new SKColor(data[i + 2], data[i + 1], data[i]));
            }
        }
        return true;
    }

    private static bool FillFromRgba16(SKBitmap bitmap, IImage decoded)
    {
        var data = decoded.Data;
        for (int y = 0; y < decoded.Height; y++)
        {
            int row = y * decoded.Stride;
            for (int x = 0; x < decoded.Width; x++)
            {
                int i = row + x * 2;
                int packed = data[i] | (data[i + 1] << 8);
                bitmap.SetPixel(x, y, new SKColor(
                    (byte)(((packed >> 8) & 0xF) * 17),
                    (byte)(((packed >> 4) & 0xF) * 17),
                    (byte)((packed & 0xF) * 17),
                    (byte)(((packed >> 12) & 0xF) * 17)));
            }
        }
        return true;
    }

    private static bool FillFromR5G6B5(SKBitmap bitmap, IImage decoded)
    {
        var data = decoded.Data;
        for (int y = 0; y < decoded.Height; y++)
        {
            int row = y * decoded.Stride;
            for (int x = 0; x < decoded.Width; x++)
            {
                int i = row + x * 2;
                int packed = data[i] | (data[i + 1] << 8);
                bitmap.SetPixel(x, y, new SKColor(
                    (byte)(((packed >> 11) & 0x1F) * 255 / 31),
                    (byte)(((packed >> 5) & 0x3F) * 255 / 63),
                    (byte)((packed & 0x1F) * 255 / 31)));
            }
        }
        return true;
    }

    private static bool FillFromGray8(SKBitmap bitmap, IImage decoded)
    {
        var data = decoded.Data;
        for (int y = 0; y < decoded.Height; y++)
        {
            int row = y * decoded.Stride;
            for (int x = 0; x < decoded.Width; x++)
            {
                byte value = data[row + x];
                bitmap.SetPixel(x, y, new SKColor(value, value, value));
            }
        }
        return true;
    }

    private static bool HasTransparency(SKBitmap bitmap)
    {
        if (bitmap.AlphaType == SKAlphaType.Opaque)
            return false;

        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Alpha != 255)
                    return true;

        return false;
    }
}
