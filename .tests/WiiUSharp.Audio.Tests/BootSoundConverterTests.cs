using System.Text;
using NAudio.Wave;

namespace WiiUSharp.Audio.Tests;

[TestClass]
public class BootSoundConverterTests
{
    [TestMethod]
    public void BtsndInputIsPassedThrough()
    {
        var path = TempFile(".btsnd");
        try
        {
            new BootSound(new short[] { 1, 2, 3, 4 }, BootSoundTarget.Tv, loopStart: 1).Save(path);

            var sound = BootSoundConverter.Load(path);

            Assert.AreEqual(BootSoundTarget.Tv, sound.Target);
            Assert.AreEqual(1u, sound.LoopStart);
            CollectionAssert.AreEqual(new short[] { 1, 2, 3, 4 }, sound.Samples);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ConvertWritesAFileBootSoundReadsBack()
    {
        var wav = TempFile(".wav");
        var btsnd = TempFile(".btsnd");
        try
        {
            WriteWav(wav, new WaveFormat(48000, 16, 2), Sine(48000, 2, 1.0, 0.25));

            BootSoundConverter.Convert(wav, btsnd, BootSoundTarget.GamePad);
            var sound = BootSound.Load(btsnd);

            Assert.AreEqual(BootSoundTarget.GamePad, sound.Target);
            Assert.AreEqual(48000, sound.FrameCount);
        }
        finally
        {
            File.Delete(wav);
            File.Delete(btsnd);
        }
    }

    [TestMethod]
    public void MatchingStereoWavIsCopiedSampleForSample()
    {
        var wav = TempFile(".wav");
        try
        {
            var samples = Sine(48000, 2, 0.5, 0.5);
            WriteWav(wav, new WaveFormat(48000, 16, 2), samples);

            var sound = BootSoundConverter.Load(wav);

            CollectionAssert.AreEqual(samples, sound.Samples);
        }
        finally
        {
            File.Delete(wav);
        }
    }

    [TestMethod]
    public void MonoLowRateWavBecomesStereo48kTrimmedToSixSeconds()
    {
        var wav = TempFile(".wav");
        try
        {
            WriteWav(wav, new WaveFormat(22050, 16, 1), Sine(22050, 1, 8.0, 0.5));

            var sound = BootSoundConverter.Load(wav);

            Assert.AreEqual(48000 * 6, sound.FrameCount);
            Assert.AreEqual(TimeSpan.FromSeconds(6), sound.Duration);
            var pcm = sound.Samples;
            for (var i = 0; i < pcm.Length; i += 2)
                Assert.AreEqual(pcm[i], pcm[i + 1], "left and right differ at frame " + i / 2);
            var rms = Math.Sqrt(pcm.Skip(48000).Take(48000).Select(s => (double)s * s).Average());
            Assert.IsTrue(Math.Abs(rms - 0.5 * 32768 / Math.Sqrt(2)) < 400, $"rms {rms}");
        }
        finally
        {
            File.Delete(wav);
        }
    }

    [TestMethod]
    public void UnsupportedExtensionAndTooManyChannelsAreRejected()
    {
        var ogg = TempFile(".ogg");
        var wav = TempFile(".wav");
        try
        {
            File.WriteAllBytes(ogg, new byte[16]);
            WriteWav(wav, new WaveFormat(48000, 16, 4), new short[48000 * 4]);

            Assert.ThrowsExactly<NotSupportedException>(() => BootSoundConverter.Load(ogg));
            Assert.ThrowsExactly<NotSupportedException>(() => BootSoundConverter.Load(wav));
        }
        finally
        {
            File.Delete(ogg);
            File.Delete(wav);
        }
    }

    [TestMethod]
    public void WavWithExtraChunksBeforeDataIsReadCorrectly()
    {
        var wav = TempFile(".wav");
        try
        {
            var samples = Sine(48000, 2, 0.25, 0.5);
            WriteWavWithListChunk(wav, samples);

            var sound = BootSoundConverter.Load(wav);

            CollectionAssert.AreEqual(samples, sound.Samples);
        }
        finally
        {
            File.Delete(wav);
        }
    }

    private static short[] Sine(int rate, int channels, double seconds, double amplitude)
    {
        var frames = (int)(rate * seconds);
        var samples = new short[frames * channels];
        for (var f = 0; f < frames; f++)
        {
            var value = (short)Math.Round(Math.Sin(2 * Math.PI * 440 * f / rate) * amplitude * 32767);
            for (var c = 0; c < channels; c++)
                samples[f * channels + c] = value;
        }
        return samples;
    }

    private static string TempFile(string extension) =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);

    private static void WriteWav(string path, WaveFormat format, short[] samples)
    {
        using var writer = new WaveFileWriter(path, format);
        writer.WriteSamples(samples, 0, samples.Length);
    }

    private static void WriteWavWithListChunk(string path, short[] samples)
    {
        var data = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, data, 0, data.Length);
        var list = Encoding.ASCII.GetBytes("INFOISFT\0\0\0test");
        using var file = File.Create(path);
        using var w = new BinaryWriter(file);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(4 + 8 + 16 + 8 + list.Length + 8 + data.Length);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);
        w.Write((short)2);
        w.Write(48000);
        w.Write(48000 * 4);
        w.Write((short)4);
        w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("LIST"));
        w.Write(list.Length);
        w.Write(list);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(data.Length);
        w.Write(data);
    }
}
