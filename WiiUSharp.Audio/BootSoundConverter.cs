using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;

namespace WiiUSharp.Audio;

/// <summary>
/// Makes a <see cref="BootSound"/> from ordinary audio files.
/// </summary>
public static class BootSoundConverter
{
    /// <summary>
    /// Loads a source and writes bootSound.btsnd.
    /// </summary>
    /// <param name="sourcePath">WAV, MP3, AIFF or an existing .btsnd.</param>
    /// <param name="destinationPath">Output path.</param>
    /// <param name="target">Where it plays.</param>
    public static void Convert(string sourcePath, string destinationPath, BootSoundTarget target = BootSoundTarget.Both) =>
        Load(sourcePath, target).Save(destinationPath);

    /// <summary>
    /// Resamples to 48 kHz stereo, trims to <see cref="BootSound.MaxSeconds"/> and packs the result.
    /// </summary>
    /// <param name="source">Any PCM or float stream.</param>
    /// <param name="target">Where it plays.</param>
    /// <exception cref="NotSupportedException">More than two channels.</exception>
    public static BootSound FromWave(WaveStream source, BootSoundTarget target = BootSoundTarget.Both)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        var samples = source.ToSampleProvider();
        samples = samples.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(samples),
            2 => samples,
            _ => throw new NotSupportedException($"{samples.WaveFormat.Channels} channels; only mono and stereo are supported."),
        };
        if (samples.WaveFormat.SampleRate != BootSound.SampleRate)
            samples = new WdlResamplingSampleProvider(samples, BootSound.SampleRate);
        samples = new OffsetSampleProvider(samples) { Take = TimeSpan.FromSeconds(BootSound.MaxSeconds) };

        return new BootSound(ReadAll(samples), target);
    }

    /// <summary>
    /// Loads a file by extension: .wav, .mp3, .aif/.aiff, or .btsnd as-is.
    /// </summary>
    /// <param name="path">Source path.</param>
    /// <param name="target">Where it plays; ignored for .btsnd input.</param>
    /// <exception cref="NotSupportedException">Unknown extension.</exception>
    public static BootSound Load(string path, BootSoundTarget target = BootSoundTarget.Both)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".btsnd")
            return BootSound.Load(path);

        using WaveStream reader = extension switch
        {
            ".wav" => new WaveFileReader(path),
            ".mp3" => new Mp3FileReaderBase(path, format => new Mp3FrameDecompressor(format)),
            ".aif" or ".aiff" => new AiffFileReader(path),
            _ => throw new NotSupportedException($"'{extension}' is not a supported boot sound source."),
        };
        return FromWave(reader, target);
    }

    private static short[] ReadAll(ISampleProvider samples)
    {
        var result = new List<short>();
        var buffer = new float[BootSound.SampleRate * BootSound.Channels];
        int read;
        while ((read = samples.Read(buffer, 0, buffer.Length)) > 0)
            for (var i = 0; i < read; i++)
                result.Add(ToPcm16(buffer[i]));
        return result.ToArray();
    }

    private static short ToPcm16(float sample) =>
        (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(sample * 32768f)));
}
