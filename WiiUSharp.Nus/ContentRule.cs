using System.Text.RegularExpressions;

namespace WiiUSharp.Nus;

/// <summary>
/// Assigns files whose title-relative path matches a pattern to a content.
/// </summary>
public sealed class ContentRule
{
    private readonly Regex _regex;

    /// <summary>
    /// Creates a new instance of the <see cref="ContentRule"/> class.
    /// </summary>
    /// <param name="pattern">Regex matched against the whole path, e.g. /code/app.xml.</param>
    /// <param name="details">How the content is built.</param>
    /// <param name="contentPerMatch">One content per matching file instead of one for all.</param>
    public ContentRule(string pattern, ContentDetails details, bool contentPerMatch = false)
    {
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        Details = details ?? throw new ArgumentNullException(nameof(details));
        ContentPerMatch = contentPerMatch;
        _regex = new Regex("^(?:" + pattern + ")$", RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// One content per matching file.
    /// </summary>
    public bool ContentPerMatch { get; }
    /// <summary>
    /// How the content is built.
    /// </summary>
    public ContentDetails Details { get; }
    /// <summary>
    /// Pattern matched against the whole path.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// True when the path matches the pattern completely.
    /// </summary>
    /// <param name="path">Title-relative path with forward slashes, e.g. /meta/meta.xml.</param>
    public bool Matches(string path) => _regex.IsMatch(path ?? throw new ArgumentNullException(nameof(path)));

    /// <inheritdoc/>
    public override string ToString() => Pattern;
}
