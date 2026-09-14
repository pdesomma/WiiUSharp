namespace WiiUSharp.Tests;

[TestClass]
public class ImageSlotTests
{
    [TestMethod]
    public void AllListsEverySlotOnce()
    {
        CollectionAssert.AreEquivalent(
            new[] { ImageSlot.BootDrc, ImageSlot.BootLogo, ImageSlot.BootTv, ImageSlot.Icon },
            ImageSlot.All.ToList());
    }

    [TestMethod]
    public void FileNameIsNameWithTgaExtension()
    {
        Assert.AreEqual("iconTex.tga", ImageSlot.Icon.FileName);
        Assert.AreEqual("bootTvTex.tga", ImageSlot.BootTv.FileName);
        Assert.AreEqual("bootDrcTex.tga", ImageSlot.BootDrc.FileName);
        Assert.AreEqual("bootLogoTex.tga", ImageSlot.BootLogo.FileName);
    }

    [TestMethod]
    public void SlotsCarryTheRequiredDimensions()
    {
        Assert.AreEqual((128, 128, 32), (ImageSlot.Icon.Width, ImageSlot.Icon.Height, ImageSlot.Icon.BitDepth));
        Assert.AreEqual((1280, 720, 24), (ImageSlot.BootTv.Width, ImageSlot.BootTv.Height, ImageSlot.BootTv.BitDepth));
        Assert.AreEqual((854, 480, 24), (ImageSlot.BootDrc.Width, ImageSlot.BootDrc.Height, ImageSlot.BootDrc.BitDepth));
        Assert.AreEqual((170, 42, 32), (ImageSlot.BootLogo.Width, ImageSlot.BootLogo.Height, ImageSlot.BootLogo.BitDepth));
    }
}
