namespace WiiUSharp;

/// <summary>
/// One of the images a title ships in its meta folder.
/// </summary>
public sealed class ImageSlot
{
    /// <summary>
    /// GamePad boot screen.
    /// </summary>
    public static readonly ImageSlot BootDrc = new("bootDrcTex", 854, 480, 24);
    /// <summary>
    /// Boot logo.
    /// </summary>
    public static readonly ImageSlot BootLogo = new("bootLogoTex", 170, 42, 32);
    /// <summary>
    /// TV boot screen.
    /// </summary>
    public static readonly ImageSlot BootTv = new("bootTvTex", 1280, 720, 24);
    /// <summary>
    /// Menu icon.
    /// </summary>
    public static readonly ImageSlot Icon = new("iconTex", 128, 128, 32);

    private ImageSlot(string name, int width, int height, int bitDepth)
    {
        Name = name;
        Width = width;
        Height = height;
        BitDepth = bitDepth;
    }

    /// <summary>
    /// Every slot.
    /// </summary>
    public static IReadOnlyList<ImageSlot> All { get; } = new[] { BootDrc, BootLogo, BootTv, Icon };
    /// <summary>
    /// Required bits per pixel.
    /// </summary>
    public int BitDepth { get; }
    /// <summary>
    /// File name in the meta folder.
    /// </summary>
    public string FileName => Name + ".tga";
    /// <summary>
    /// Required height in pixels.
    /// </summary>
    public int Height { get; }
    /// <summary>
    /// Name without extension.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Required width in pixels.
    /// </summary>
    public int Width { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
