using SkiaSharp;
using TargaSharp;

namespace WiiUSharp.Imaging;

/// <summary>
/// Produces the TGA a title slot expects: exact size, exact depth, uncompressed, bare TGA 2.0 footer with no extension area, as retail titles carry.
/// </summary>
public static class TitleImage
{
    /// <summary>
    /// Loads any source and writes the slot's TGA.
    /// </summary>
    /// <param name="sourcePath">PNG, JPEG, BMP, WebP or TGA.</param>
    /// <param name="slot">Target slot.</param>
    /// <param name="destinationPath">Output TGA path.</param>
    public static void Convert(string sourcePath, ImageSlot slot, string destinationPath)
    {
        var tga = Load(sourcePath, slot);
        tga.Save(destinationPath);
    }

    /// <summary>
    /// Resizes a bitmap to the slot and packs it as TGA.
    /// </summary>
    /// <param name="bitmap">Any color type; converted as needed.</param>
    /// <param name="slot">Target slot.</param>
    public static TgaFile FromBitmap(SKBitmap bitmap, ImageSlot slot)
    {
        if (bitmap is null)
            throw new ArgumentNullException(nameof(bitmap));
        if (slot is null)
            throw new ArgumentNullException(nameof(slot));

        using var fitted = Fit(bitmap, slot);
        return Pack(fitted, slot);
    }

    /// <summary>
    /// Returns the TGA unchanged apart from the footer shape when it already fits the slot; otherwise resizes and converts it.
    /// </summary>
    /// <param name="tga">Source image.</param>
    /// <param name="slot">Target slot.</param>
    public static TgaFile FromTga(TgaFile tga, ImageSlot slot)
    {
        if (tga is null)
            throw new ArgumentNullException(nameof(tga));
        if (slot is null)
            throw new ArgumentNullException(nameof(slot));

        if (Problems(tga, slot).All(p => p == FooterProblem || p == ExtensionProblem))
        {
            var copy = tga.Clone();
            copy.DeveloperArea = null;
            copy.ToNewFormat(false);
            return copy;
        }

        using var bitmap = ToBitmap(tga);
        return FromBitmap(bitmap, slot);
    }

    /// <summary>
    /// Loads any source as the slot's TGA.
    /// </summary>
    /// <param name="path">PNG, JPEG, BMP, WebP or TGA.</param>
    /// <param name="slot">Target slot.</param>
    /// <exception cref="InvalidDataException">The file could not be decoded.</exception>
    public static TgaFile Load(string path, ImageSlot slot)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (slot is null)
            throw new ArgumentNullException(nameof(slot));

        if (string.Equals(Path.GetExtension(path), ".tga", StringComparison.OrdinalIgnoreCase))
            return FromTga(new TgaFile(path), slot);

        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream) ?? throw new InvalidDataException($"'{path}' is not a supported image.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = SKBitmap.Decode(codec, info) ?? throw new InvalidDataException($"'{path}' could not be decoded.");
        return FromBitmap(bitmap, slot);
    }

    /// <summary>
    /// Everything that stops the TGA being used for the slot; empty when it fits.
    /// </summary>
    /// <param name="tga">Image to check.</param>
    /// <param name="slot">Target slot.</param>
    public static IReadOnlyList<string> Problems(TgaFile tga, ImageSlot slot)
    {
        if (tga is null)
            throw new ArgumentNullException(nameof(tga));
        if (slot is null)
            throw new ArgumentNullException(nameof(slot));

        var problems = new List<string>();
        if (tga.Width != slot.Width || tga.Height != slot.Height)
            problems.Add($"Size is {tga.Width}x{tga.Height}; {slot.Name} needs {slot.Width}x{slot.Height}.");
        if ((int)tga.Header.ImageSpec.PixelDepth != slot.BitDepth)
            problems.Add($"Depth is {(int)tga.Header.ImageSpec.PixelDepth} bpp; {slot.Name} needs {slot.BitDepth}.");
        if (tga.Header.ImageType != TgaImageType.UncompressedTrueColor)
            problems.Add($"Image type is {tga.Header.ImageType}; {slot.Name} needs uncompressed true color.");
        if (tga.Footer is null)
            problems.Add(FooterProblem);
        if (tga.ExtensionArea is not null || tga.DeveloperArea is not null)
            problems.Add(ExtensionProblem);
        return problems;
    }

    /// <summary>
    /// Throws unless the TGA fits the slot exactly.
    /// </summary>
    /// <param name="tga">Image to check.</param>
    /// <param name="slot">Target slot.</param>
    /// <exception cref="InvalidDataException">One or more problems, all listed in the message.</exception>
    public static void Verify(TgaFile tga, ImageSlot slot)
    {
        var problems = Problems(tga, slot);
        if (problems.Count > 0)
            throw new InvalidDataException(string.Join(" ", problems));
    }

    private const string ExtensionProblem = "Has a TGA extension or developer area.";
    private const string FooterProblem = "Missing the TGA 2.0 footer.";

    private static SKBitmap Fit(SKBitmap bitmap, ImageSlot slot)
    {
        var info = new SKImageInfo(slot.Width, slot.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        if (bitmap.Width == slot.Width && bitmap.Height == slot.Height && bitmap.ColorType == SKColorType.Bgra8888 && bitmap.AlphaType == SKAlphaType.Unpremul)
            return bitmap.Copy();

        var resized = bitmap.Resize(info, new SKSamplingOptions(SKCubicResampler.CatmullRom));
        return resized ?? throw new InvalidOperationException("Resize failed.");
    }

    private static TgaFile Pack(SKBitmap bitmap, ImageSlot slot)
    {
        var bytesPerPixel = slot.BitDepth / 8;
        var tga = new TgaFile(
            (ushort)slot.Width,
            (ushort)slot.Height,
            slot.BitDepth == 32 ? TgaPixelDepth.Bpp32 : TgaPixelDepth.Bpp24,
            TgaImageType.UncompressedTrueColor,
            attrBits: (byte)(slot.BitDepth == 32 ? 8 : 0),
            newFormat: false);
        tga.Header.ImageSpec.ImageDescriptor.ImageOrigin = TgaImageOrigin.BottomLeft;
        tga.ToNewFormat(false);

        var data = new byte[slot.Width * slot.Height * bytesPerPixel];
        var source = bitmap.Bytes;
        var rowBytes = bitmap.RowBytes;
        for (var y = 0; y < slot.Height; y++)
        {
            var sourceRow = (slot.Height - 1 - y) * rowBytes;
            var targetRow = y * slot.Width * bytesPerPixel;
            for (var x = 0; x < slot.Width; x++)
            {
                var s = sourceRow + x * 4;
                var t = targetRow + x * bytesPerPixel;
                data[t] = source[s];
                data[t + 1] = source[s + 1];
                data[t + 2] = source[s + 2];
                if (bytesPerPixel == 4)
                    data[t + 3] = source[s + 3];
            }
        }
        tga.ImageArea.ImageData = data;
        return tga;
    }

    private static SKBitmap ToBitmap(TgaFile tga)
    {
        var depth = (int)tga.Header.ImageSpec.PixelDepth;
        if (tga.Header.ImageType is not (TgaImageType.UncompressedTrueColor or TgaImageType.RleTrueColor) || depth is not (24 or 32))
            throw new NotSupportedException("Only 24 and 32 bpp true-color TGA sources are supported.");

        var data = tga.ImageArea.ImageData ?? throw new InvalidDataException("TGA has no image data.");
        var bytesPerPixel = depth / 8;
        var bitmap = new SKBitmap(new SKImageInfo(tga.Width, tga.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        var origin = tga.Header.ImageSpec.ImageDescriptor.ImageOrigin;
        var flipVertical = origin is TgaImageOrigin.BottomLeft or TgaImageOrigin.BottomRight;
        var flipHorizontal = origin is TgaImageOrigin.BottomRight or TgaImageOrigin.TopRight;
        var pixels = new byte[tga.Width * tga.Height * 4];
        for (var y = 0; y < tga.Height; y++)
        {
            var sourceY = flipVertical ? tga.Height - 1 - y : y;
            for (var x = 0; x < tga.Width; x++)
            {
                var sourceX = flipHorizontal ? tga.Width - 1 - x : x;
                var s = (sourceY * tga.Width + sourceX) * bytesPerPixel;
                var t = (y * tga.Width + x) * 4;
                pixels[t] = data[s];
                pixels[t + 1] = data[s + 1];
                pixels[t + 2] = data[s + 2];
                pixels[t + 3] = bytesPerPixel == 4 ? data[s + 3] : (byte)0xFF;
            }
        }
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        return bitmap;
    }
}
