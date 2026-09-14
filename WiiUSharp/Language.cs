namespace WiiUSharp;

/// <summary>
/// Wii U system languages.
/// </summary>
public enum Language
{
    Japanese,
    English,
    French,
    German,
    Italian,
    Spanish,
    SimplifiedChinese,
    Korean,
    Dutch,
    Portuguese,
    Russian,
    TraditionalChinese,
}

/// <summary>
/// <see cref="Language"/> extensions.
/// </summary>
public static class LanguageExtensions
{
    /// <summary>
    /// Wii U locale code, e.g. "en" or "zhs".
    /// </summary>
    /// <param name="language">Language.</param>
    /// <exception cref="ArgumentOutOfRangeException">Undefined language.</exception>
    public static string Code(this Language language) => language switch
    {
        Language.Japanese => "ja",
        Language.English => "en",
        Language.French => "fr",
        Language.German => "de",
        Language.Italian => "it",
        Language.Spanish => "es",
        Language.SimplifiedChinese => "zhs",
        Language.Korean => "ko",
        Language.Dutch => "nl",
        Language.Portuguese => "pt",
        Language.Russian => "ru",
        Language.TraditionalChinese => "zht",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown Wii U language."),
    };
}
