namespace WiiUSharp.Tests;

[TestClass]
public class BootSoundTests
{
    [TestMethod]
    public void ParseReadsBackWhatToBytesWrote()
    {
        var original = new BootSound(new short[] { 1, -1, short.MaxValue, short.MinValue, 0x1234, -0x1234 }, BootSoundTarget.GamePad, loopStart: 2);

        var parsed = BootSound.Parse(original.ToBytes());

        Assert.AreEqual(BootSoundTarget.GamePad, parsed.Target);
        Assert.AreEqual(2u, parsed.LoopStart);
        Assert.AreEqual(3, parsed.FrameCount);
        CollectionAssert.AreEqual(original.Samples, parsed.Samples);
    }

    [TestMethod]
    public void ParseRejectsShortOrRaggedInput()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => BootSound.Parse(new byte[7]));
        Assert.ThrowsExactly<InvalidDataException>(() => BootSound.Parse(new byte[10]));
    }

    [TestMethod]
    public void RejectsOddSamplesAndLoopPastTheEnd()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new BootSound(new short[3]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BootSound(new short[4], loopStart: 3));
    }

    [TestMethod]
    public void SamplesAreCopiedInAndOut()
    {
        var samples = new short[] { 5, 6 };
        var sound = new BootSound(samples);
        samples[0] = 99;

        var copy = sound.Samples;
        copy[1] = 99;

        CollectionAssert.AreEqual(new short[] { 5, 6 }, sound.Samples);
    }

    [TestMethod]
    public void ToBytesMatchesTheInjectorsLayout()
    {
        var bytes = new BootSound(new short[] { 0x0102, unchecked((short)0xFFFE) }).ToBytes();

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 2, 0, 0, 0, 0, 0x01, 0x02, 0xFF, 0xFE }, bytes);
    }

    [TestMethod]
    public void DurationFollowsTheFrameCount()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(1), new BootSound(new short[BootSound.SampleRate * 2]).Duration);
    }
}
