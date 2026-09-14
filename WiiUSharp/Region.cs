namespace WiiUSharp;

/// <summary>
/// Regions a title may run in.
/// </summary>
[Flags]
public enum Region : uint
{
    None = 0,
    Japan = 1 << 0,
    UnitedStates = 1 << 1,
    Europe = 1 << 2,
    Australia = 1 << 3,
    China = 1 << 4,
    Korea = 1 << 5,
    Taiwan = 1 << 6,
    /// <summary>
    /// Region-free.
    /// </summary>
    All = 0xFFFFFFFF,
}
