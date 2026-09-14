namespace WiiUSharp;

/// <summary>
/// High 32 bits of a <see cref="TitleId"/>.
/// </summary>
public enum TitleType : uint
{
    /// <summary>
    /// Retail and eShop games.
    /// </summary>
    Game = 0x00050000,
    /// <summary>
    /// Demos; used by Virtual Console injections.
    /// </summary>
    Demo = 0x00050002,
    /// <summary>
    /// Downloadable content.
    /// </summary>
    Dlc = 0x0005000C,
    /// <summary>
    /// Title updates.
    /// </summary>
    Update = 0x0005000E,
    /// <summary>
    /// System applications.
    /// </summary>
    SystemApplication = 0x00050010,
    /// <summary>
    /// System data.
    /// </summary>
    SystemData = 0x0005001B,
    /// <summary>
    /// System applets.
    /// </summary>
    SystemApplet = 0x00050030,
}
