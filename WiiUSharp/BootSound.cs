namespace WiiUSharp;

/// <summary>
/// meta/bootSound.btsnd: an 8-byte header and big-endian 16-bit stereo PCM at 48 kHz.
/// </summary>
public sealed class BootSound
{
    /// <summary>
    /// Bits per sample.
    /// </summary>
    public const int BitsPerSample = 16;
    /// <summary>
    /// Channel count.
    /// </summary>
    public const int Channels = 2;
    /// <summary>
    /// File name in the meta folder.
    /// </summary>
    public const string FileName = "bootSound.btsnd";
    /// <summary>
    /// Header length.
    /// </summary>
    public const int HeaderSize = 8;
    /// <summary>
    /// Longest playback in seconds.
    /// </summary>
    public const int MaxSeconds = 6;
    /// <summary>
    /// Samples per second.
    /// </summary>
    public const int SampleRate = 48000;

    private readonly short[] _samples;

    /// <summary>
    /// Creates a new instance of the <see cref="BootSound"/> class.
    /// </summary>
    /// <param name="samples">Interleaved left/right samples.</param>
    /// <param name="target">Where it plays.</param>
    /// <param name="loopStart">Frame to loop back to.</param>
    /// <exception cref="ArgumentException">Odd sample count.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Loop start past the end.</exception>
    public BootSound(short[] samples, BootSoundTarget target = BootSoundTarget.Both, uint loopStart = 0)
    {
        if (samples is null)
            throw new ArgumentNullException(nameof(samples));
        if (samples.Length % Channels != 0)
            throw new ArgumentException("Samples must be interleaved stereo pairs.", nameof(samples));
        if (loopStart > samples.Length / Channels)
            throw new ArgumentOutOfRangeException(nameof(loopStart), loopStart, "Loop start is past the end.");

        _samples = (short[])samples.Clone();
        Target = target;
        LoopStart = loopStart;
    }

    /// <summary>
    /// Playback length.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromSeconds((double)FrameCount / SampleRate);
    /// <summary>
    /// Stereo frames.
    /// </summary>
    public int FrameCount => _samples.Length / Channels;
    /// <summary>
    /// Frame to loop back to.
    /// </summary>
    public uint LoopStart { get; }
    /// <summary>
    /// Copy of the interleaved samples.
    /// </summary>
    public short[] Samples => (short[])_samples.Clone();
    /// <summary>
    /// Where it plays.
    /// </summary>
    public BootSoundTarget Target { get; }

    /// <summary>
    /// Parses a .btsnd file.
    /// </summary>
    /// <param name="bytes">Whole file.</param>
    /// <exception cref="InvalidDataException">Too short or an odd number of sample bytes.</exception>
    public static BootSound Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < HeaderSize)
            throw new InvalidDataException("Shorter than the header.");
        if ((bytes.Length - HeaderSize) % (Channels * 2) != 0)
            throw new InvalidDataException("Sample data is not whole stereo frames.");

        var target = ReadUInt32(bytes, 0);
        var loopStart = ReadUInt32(bytes, 4);
        var samples = new short[(bytes.Length - HeaderSize) / 2];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (short)(bytes[HeaderSize + i * 2] << 8 | bytes[HeaderSize + i * 2 + 1]);
        return new BootSound(samples, (BootSoundTarget)target, loopStart);
    }

    /// <summary>
    /// Reads a .btsnd file.
    /// </summary>
    /// <param name="path">File path.</param>
    public static BootSound Load(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>
    /// Writes the file.
    /// </summary>
    /// <param name="path">Destination.</param>
    public void Save(string path) => File.WriteAllBytes(path, ToBytes());

    /// <summary>
    /// The file contents.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[HeaderSize + _samples.Length * 2];
        WriteUInt32(bytes, 0, (uint)Target);
        WriteUInt32(bytes, 4, LoopStart);
        for (var i = 0; i < _samples.Length; i++)
        {
            bytes[HeaderSize + i * 2] = (byte)(_samples[i] >> 8);
            bytes[HeaderSize + i * 2 + 1] = (byte)_samples[i];
        }
        return bytes;
    }

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
