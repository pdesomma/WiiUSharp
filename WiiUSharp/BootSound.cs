namespace WiiUSharp;

/// <summary>
/// Format of the sound played while a title boots.
/// </summary>
public static class BootSound
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
    /// Longest playback in seconds.
    /// </summary>
    public const int MaxSeconds = 6;
    /// <summary>
    /// Samples per second.
    /// </summary>
    public const int SampleRate = 48000;
}
