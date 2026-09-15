using SkiaSharp;
using TargaSharp;

namespace WiiUSharp.Imaging.Tests;

[TestClass]
public class TitleImageTests
{
    private static readonly SKColor Blue = new(0, 0, 255);
    private static readonly SKColor Red = new(255, 0, 0);

    [TestMethod]
    public void ConvertWritesAFileTargaSharpReadsBack()
    {
        var png = TempFile(".png");
        var tga = TempFile(".tga");
        try
        {
            SavePng(TopRedBottomBlue(64, 64), png);

            TitleImage.Convert(png, ImageSlot.BootLogo, tga);
            var loaded = new TgaFile(tga);

            Assert.AreEqual(170, loaded.Width);
            Assert.AreEqual(42, loaded.Height);
            Assert.AreEqual(TgaPixelDepth.Bpp32, loaded.Header.ImageSpec.PixelDepth);
            Assert.IsNotNull(loaded.Footer);
            Assert.IsNull(loaded.ExtensionArea);
            var bytes = File.ReadAllBytes(tga);
            Assert.AreEqual(18 + 170 * 42 * 4 + 26, bytes.Length, "header, pixels, bare footer: what retail titles carry");
            Assert.AreEqual("TRUEVISION-XFILE.\0", System.Text.Encoding.ASCII.GetString(bytes, bytes.Length - 18, 18));
        }
        finally
        {
            File.Delete(png);
            File.Delete(tga);
        }
    }

    [TestMethod]
    public void FromBitmapDropsAlphaForTwentyFourBitSlots()
    {
        using var bitmap = TopRedBottomBlue(32, 32);

        var tga = TitleImage.FromBitmap(bitmap, ImageSlot.BootTv);

        Assert.AreEqual(TgaPixelDepth.Bpp24, tga.Header.ImageSpec.PixelDepth);
        Assert.AreEqual(0, tga.Header.ImageSpec.ImageDescriptor.AlphaChannelBits);
        Assert.AreEqual(1280 * 720 * 3, tga.ImageArea.ImageData!.Length);
        CollectionAssert.AreEqual(new byte[] { 255, 0, 0 }, tga.ImageArea.ImageData.Take(3).ToArray());
    }

    [TestMethod]
    public void FromTgaConvertsWhenTheSourceDoesNotFit()
    {
        var small = TitleImage.FromBitmap(TopRedBottomBlue(16, 16), ImageSlot.BootLogo);

        var icon = TitleImage.FromTga(small, ImageSlot.Icon);

        Assert.AreEqual(0, TitleImage.Problems(icon, ImageSlot.Icon).Count);
        CollectionAssert.AreEqual(new byte[] { 255, 0, 0, 255 }, Pixel(icon, 0, 0));
        CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, Pixel(icon, 0, 127));
    }

    [TestMethod]
    public void FromTgaKeepsPixelsAndDropsTheExtensionAreaWhenItAlreadyFits()
    {
        var source = new TgaFile(128, 128, TgaPixelDepth.Bpp32, TgaImageType.UncompressedTrueColor, attrBits: 8, newFormat: true);
        source.ImageArea.ImageData = Enumerable.Range(0, 128 * 128 * 4).Select(i => (byte)i).ToArray();
        Assert.IsNotNull(source.ExtensionArea);

        var result = TitleImage.FromTga(source, ImageSlot.Icon);

        Assert.IsNotNull(result.Footer);
        Assert.IsNull(result.ExtensionArea);
        CollectionAssert.AreEqual(source.ImageArea.ImageData, result.ImageArea.ImageData);
        Assert.IsNotNull(source.ExtensionArea, "the source is untouched");
    }

    [TestMethod]
    public void FromTgaAddsTheFooterToAnOldFormatSourceThatAlreadyFits()
    {
        var source = new TgaFile(128, 128, TgaPixelDepth.Bpp32, TgaImageType.UncompressedTrueColor, attrBits: 8, newFormat: false);
        source.ImageArea.ImageData = Enumerable.Range(0, 128 * 128 * 4).Select(i => (byte)i).ToArray();

        var result = TitleImage.FromTga(source, ImageSlot.Icon);

        Assert.IsNotNull(result.Footer);
        Assert.IsNull(result.ExtensionArea);
        Assert.AreEqual(0, TitleImage.Problems(result, ImageSlot.Icon).Count);
        CollectionAssert.AreEqual(source.ImageArea.ImageData, result.ImageArea.ImageData);
    }

    [TestMethod]
    public void FromTgaHonoursTheSourceOrigin()
    {
        var topLeft = new TgaFile(128, 128, TgaPixelDepth.Bpp24, TgaImageType.UncompressedTrueColor, attrBits: 0, newFormat: false);
        topLeft.Header.ImageSpec.ImageDescriptor.ImageOrigin = TgaImageOrigin.TopLeft;
        var data = new byte[128 * 128 * 3];
        for (var i = 0; i < 128 * 3; i++)
            data[i] = 0xAA;
        topLeft.ImageArea.ImageData = data;

        var icon = TitleImage.FromTga(topLeft, ImageSlot.Icon);

        Assert.AreEqual(TgaImageOrigin.BottomLeft, icon.Header.ImageSpec.ImageDescriptor.ImageOrigin);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xAA, 0xAA, 0xFF }, Pixel(icon, 0, 127));
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0xFF }, Pixel(icon, 0, 0));
    }

    [TestMethod]
    public void LoadRejectsFilesThatAreNotImages()
    {
        var path = TempFile(".png");
        try
        {
            File.WriteAllText(path, "not an image");

            Assert.ThrowsExactly<InvalidDataException>(() => TitleImage.Load(path, ImageSlot.Icon));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void PngBecomesABottomLeftUncompressedTgaOfTheSlotSize()
    {
        var png = TempFile(".png");
        try
        {
            SavePng(TopRedBottomBlue(64, 64), png);

            var tga = TitleImage.Load(png, ImageSlot.Icon);

            Assert.AreEqual(128, tga.Width);
            Assert.AreEqual(128, tga.Height);
            Assert.AreEqual(TgaPixelDepth.Bpp32, tga.Header.ImageSpec.PixelDepth);
            Assert.AreEqual(TgaImageType.UncompressedTrueColor, tga.Header.ImageType);
            Assert.AreEqual(8, tga.Header.ImageSpec.ImageDescriptor.AlphaChannelBits);
            Assert.AreEqual(TgaImageOrigin.BottomLeft, tga.Header.ImageSpec.ImageDescriptor.ImageOrigin);
            Assert.IsNotNull(tga.Footer);
            Assert.IsNull(tga.ExtensionArea);
            CollectionAssert.AreEqual(new byte[] { 255, 0, 0, 255 }, Pixel(tga, 0, 0));
            CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, Pixel(tga, 127, 127));
            Assert.AreEqual(0, TitleImage.Problems(tga, ImageSlot.Icon).Count);
        }
        finally
        {
            File.Delete(png);
        }
    }

    [TestMethod]
    public void ProblemsListsEverythingWrongAndVerifyThrows()
    {
        var wrong = new TgaFile(100, 50, TgaPixelDepth.Bpp24, TgaImageType.RleTrueColor, attrBits: 0, newFormat: true);
        wrong.ImageArea.ImageData = new byte[100 * 50 * 3];

        var problems = TitleImage.Problems(wrong, ImageSlot.Icon);

        Assert.AreEqual(4, problems.Count);
        StringAssert.Contains(problems[0], "100x50");
        StringAssert.Contains(problems[1], "24 bpp");
        StringAssert.Contains(problems[2], "RleTrueColor");
        StringAssert.Contains(problems[3], "extension");
        Assert.ThrowsExactly<InvalidDataException>(() => TitleImage.Verify(wrong, ImageSlot.Icon));
    }

    [TestMethod]
    public void ProblemsReportsAMissingFooter()
    {
        var old = new TgaFile(128, 128, TgaPixelDepth.Bpp32, TgaImageType.UncompressedTrueColor, attrBits: 8, newFormat: false);
        old.ImageArea.ImageData = new byte[128 * 128 * 4];

        var problems = TitleImage.Problems(old, ImageSlot.Icon);

        Assert.AreEqual(1, problems.Count);
        StringAssert.Contains(problems[0], "footer");
    }

    private static byte[] Pixel(TgaFile tga, int x, int row)
    {
        var bytesPerPixel = (int)tga.Header.ImageSpec.PixelDepth / 8;
        var at = (row * tga.Width + x) * bytesPerPixel;
        return tga.ImageArea.ImageData!.Skip(at).Take(bytesPerPixel).ToArray();
    }

    private static void SavePng(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    private static string TempFile(string extension) =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);

    private static SKBitmap TopRedBottomBlue(int width, int height)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, y < height / 2 ? Red : Blue);
        return bitmap;
    }
}
