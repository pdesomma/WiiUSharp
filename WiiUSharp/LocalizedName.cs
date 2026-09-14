namespace WiiUSharp;

/// <summary>
/// Title name in one language.
/// </summary>
/// <param name="ShortName">Menu name.</param>
/// <param name="LongName">Banner name; may contain a newline.</param>
public sealed record LocalizedName(string ShortName, string LongName)
{
    /// <summary>
    /// Creates a new instance of the <see cref="LocalizedName"/> record with one name for both forms.
    /// </summary>
    /// <param name="name">Name.</param>
    public LocalizedName(string name) : this(name, name)
    {
    }

    /// <summary>
    /// Same name for every language.
    /// </summary>
    /// <param name="name">Name.</param>
    public static IReadOnlyDictionary<Language, LocalizedName> ForAllLanguages(LocalizedName name)
    {
        if (name is null)
            throw new ArgumentNullException(nameof(name));

        var names = new Dictionary<Language, LocalizedName>();
        foreach (Language language in Enum.GetValues(typeof(Language)))
            names[language] = name;
        return names;
    }
}
