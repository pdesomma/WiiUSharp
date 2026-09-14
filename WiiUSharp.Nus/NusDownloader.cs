using System.Globalization;
using System.Net;
using System.Net.Http;

namespace WiiUSharp.Nus;

/// <summary>
/// Fetches a title's package from the update server: TMD, ticket, every content and H3 table.
/// </summary>
/// <remarks>No keys are needed to download; a title key is needed to make a ticket when the server has none.</remarks>
public sealed class NusDownloader
{
    /// <summary>
    /// The public content server.
    /// </summary>
    public const string DefaultBaseUrl = "http://ccs.cdn.c.shop.nintendowifi.net/ccs/download/";

    private const int BufferSize = 1 << 16;
    private const long ReportEvery = 1 << 20;

    private readonly HttpClient _client;

    /// <summary>
    /// Creates a new instance of the <see cref="NusDownloader"/> class.
    /// </summary>
    /// <param name="client">Client to fetch with; the caller owns it.</param>
    /// <param name="baseUrl">Server root ending in a slash, or null for <see cref="DefaultBaseUrl"/>.</param>
    public NusDownloader(HttpClient client, string? baseUrl = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        BaseUrl = baseUrl ?? DefaultBaseUrl;
        if (!BaseUrl.EndsWith("/", StringComparison.Ordinal))
            throw new ArgumentException("Base URL must end with a slash.", nameof(baseUrl));
    }

    /// <summary>
    /// Server root the title folders hang off.
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Downloads the package into a folder; files already there at the right length are kept.
    /// </summary>
    /// <param name="titleId">Title to fetch.</param>
    /// <param name="outputDirectory">Where title.tmd, title.tik, *.app and *.h3 go.</param>
    /// <param name="titleKey">Wrapped title key for a fake ticket; null to use the server's ticket.</param>
    /// <param name="commonKey">When given, content 0 is checked to decrypt to an FST before the rest is fetched.</param>
    /// <param name="progress">Per-file byte counts.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <exception cref="InvalidOperationException">The server has no ticket and no title key was given.</exception>
    /// <exception cref="InvalidDataException">The keys do not decrypt content 0 to an FST.</exception>
    /// <exception cref="HttpRequestException">The server refused a file.</exception>
    public async Task<TmdInfo> DownloadAsync(TitleId titleId, string outputDirectory, EncryptedTitleKey? titleKey = null, CommonKey? commonKey = null, IProgress<NusDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

        Directory.CreateDirectory(outputDirectory);
        var folder = BaseUrl + titleId.Value.ToString("x16", CultureInfo.InvariantCulture) + "/";

        var tmdPath = Path.Combine(outputDirectory, NusFormat.TmdFileName);
        await Fetch(folder + "tmd", tmdPath, Report(progress, NusFormat.TmdFileName, 1, 2), cancellationToken).ConfigureAwait(false);
        var tmd = Tmd.Parse(File.ReadAllBytes(tmdPath));
        var count = 2 + tmd.Contents.Count + tmd.Contents.Count(c => c.IsHashed);

        var ticketPath = Path.Combine(outputDirectory, NusFormat.TicketFileName);
        if (titleKey is not null)
            File.WriteAllBytes(ticketPath, Ticket.Build(titleId, titleKey.Value));
        else if (!await Fetch(folder + "cetk", ticketPath, Report(progress, NusFormat.TicketFileName, 2, count), cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException($"The server has no ticket for {titleId}; supply its title key.");

        var number = 2;
        foreach (var content in tmd.Contents)
        {
            var name = NusFormat.ContentFileName(content.Id);
            var id = content.Id.ToString("X8", CultureInfo.InvariantCulture);
            await Fetch(folder + id, Path.Combine(outputDirectory, name), Report(progress, name, ++number, count), cancellationToken, content.EncryptedSize).ConfigureAwait(false);
            if (content.IsHashed)
            {
                var h3 = NusFormat.HashFileName(content.Id);
                await Fetch(folder + id + ".h3", Path.Combine(outputDirectory, h3), Report(progress, h3, ++number, count), cancellationToken).ConfigureAwait(false);
            }

            if (content.Index == 0 && commonKey is not null && !new NusUnpacker(commonKey.Value).KeysMatch(outputDirectory))
                throw new InvalidDataException("Content 0 does not decrypt to an FST; the title key or common key is wrong.");
        }
        return tmd;
    }

    /// <summary>
    /// Streams one file to disk; false on 404. A file already at the expected length is left alone.
    /// </summary>
    private async Task<bool> Fetch(string url, string path, Action<long, long?> report, CancellationToken cancellationToken, long? expectedLength = null)
    {
        if (expectedLength is not null && File.Exists(path) && new FileInfo(path).Length == expectedLength)
        {
            report(expectedLength.Value, expectedLength);
            return true;
        }

        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{url} returned {(int)response.StatusCode} {response.ReasonPhrase}.");

        var total = response.Content.Headers.ContentLength ?? expectedLength;
        report(0, total);
        using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        var buffer = new byte[BufferSize];
        long received = 0;
        long lastReport = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
            received += read;
            if (received - lastReport >= ReportEvery)
            {
                lastReport = received;
                report(received, total);
            }
        }
        report(received, total);
        return true;
    }

    private static Action<long, long?> Report(IProgress<NusDownloadProgress>? progress, string file, int number, int count) =>
        (received, total) => progress?.Report(new NusDownloadProgress(file, number, count, received, total));
}
