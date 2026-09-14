using System.Net;
using System.Net.Http;

namespace WiiUSharp.Nus.Tests;

[TestClass]
public class NusDownloaderTests
{
    private const string BaseUrl = "http://cdn.test/ccs/download/";

    private static readonly CommonKey WrongKey = new(Enumerable.Range(1, 16).Select(i => (byte)(i * 13)).ToArray());

    private string _root = null!;
    private string _package = null!;
    private string _output = null!;
    private FakeCdn _cdn = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Reference.TempRoot();
        _package = Path.Combine(_root, "package");
        _output = Path.Combine(_root, "download");
        new NusPacker(Reference.CommonKey).Pack(FakeTitle.Create(Path.Combine(_root, "title")), _package, Reference.TitleKey);
        _cdn = new FakeCdn(_package, Reference.TitleId);
        _client = new HttpClient(_cdn);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task DownloadAsync_ServerTicket_FetchesEverythingAndUnpacks()
    {
        var reports = new List<NusDownloadProgress>();

        var tmd = await new NusDownloader(_client, BaseUrl).DownloadAsync(Reference.TitleId, _output, progress: new SyncProgress<NusDownloadProgress>(reports.Add));

        Assert.AreEqual(9, tmd.Contents.Count);
        foreach (var file in Directory.GetFiles(_package).Where(f => Path.GetFileName(f) != NusFormat.CertificateFileName))
            CollectionAssert.AreEqual(File.ReadAllBytes(file), File.ReadAllBytes(Path.Combine(_output, Path.GetFileName(file))), file);
        Assert.IsFalse(File.Exists(Path.Combine(_output, NusFormat.CertificateFileName)));
        Assert.IsTrue(_cdn.Requests.Contains("cetk"));
        Assert.IsTrue(_cdn.Requests.Contains("tmd"));
        Assert.IsTrue(_cdn.Requests.Contains("00000001"));
        Assert.IsTrue(_cdn.Requests.Any(r => r.EndsWith(".h3", StringComparison.Ordinal)));

        var expectedFiles = 2 + 9 + tmd.Contents.Count(c => c.IsHashed);
        Assert.AreEqual(expectedFiles, reports.Max(r => r.FileNumber));
        Assert.IsTrue(reports.Where(r => r.File != NusFormat.TmdFileName).All(r => r.FileCount == expectedFiles));
        var last = reports.Last();
        Assert.AreEqual(last.BytesTotal, last.BytesReceived);
        Assert.AreEqual(NusFormat.TmdFileName, reports[0].File);

        new NusUnpacker(Reference.CommonKey).Unpack(_output, Path.Combine(_root, "unpacked"));
        CollectionAssert.AreEqual(FakeTitle.Files["content/sub/b.bin"], File.ReadAllBytes(Path.Combine(_root, "unpacked", "content", "sub", "b.bin")));
    }

    [TestMethod]
    public async Task DownloadAsync_NoServerTicketWithTitleKey_BuildsFakeTicket()
    {
        _cdn.HasTicket = false;
        var wrapped = Reference.TitleKey.Encrypt(Reference.TitleId, Reference.CommonKey);

        await new NusDownloader(_client, BaseUrl).DownloadAsync(Reference.TitleId, _output, wrapped, Reference.CommonKey);

        var ticket = Ticket.Parse(File.ReadAllBytes(Path.Combine(_output, NusFormat.TicketFileName)));
        Assert.AreEqual(wrapped, ticket.TitleKey);
        Assert.AreEqual(Reference.TitleId, ticket.TitleId);
        Assert.IsFalse(_cdn.Requests.Contains("cetk"));
        Assert.IsTrue(new NusUnpacker(Reference.CommonKey).KeysMatch(_output));
    }

    [TestMethod]
    public async Task DownloadAsync_NoServerTicketNoTitleKey_ThrowsInvalidOperationException()
    {
        _cdn.HasTicket = false;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new NusDownloader(_client, BaseUrl).DownloadAsync(Reference.TitleId, _output));

        Assert.IsFalse(_cdn.Requests.Contains("00000000"));
    }

    [TestMethod]
    public async Task DownloadAsync_WrongKeys_StopsAfterContentZero()
    {
        var wrapped = Reference.TitleKey.Encrypt(Reference.TitleId, WrongKey);

        var error = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new NusDownloader(_client, BaseUrl).DownloadAsync(Reference.TitleId, _output, wrapped, Reference.CommonKey));

        StringAssert.Contains(error.Message, "FST");
        Assert.IsTrue(_cdn.Requests.Contains("00000000"));
        Assert.IsFalse(_cdn.Requests.Contains("00000001"));
    }

    [TestMethod]
    public async Task DownloadAsync_ContentAlreadyComplete_IsNotFetchedAgain()
    {
        Directory.CreateDirectory(_output);
        File.Copy(Path.Combine(_package, NusFormat.ContentFileName(1)), Path.Combine(_output, NusFormat.ContentFileName(1)));
        File.WriteAllBytes(Path.Combine(_output, NusFormat.ContentFileName(2)), new byte[3]);

        await new NusDownloader(_client, BaseUrl).DownloadAsync(Reference.TitleId, _output);

        Assert.IsFalse(_cdn.Requests.Contains("00000001"));
        Assert.IsTrue(_cdn.Requests.Contains("00000002"));
        CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(_package, NusFormat.ContentFileName(2))), File.ReadAllBytes(Path.Combine(_output, NusFormat.ContentFileName(2))));
    }

    [TestMethod]
    public async Task DownloadAsync_ServerError_ThrowsHttpRequestException()
    {
        _cdn.FailWith = HttpStatusCode.InternalServerError;

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => new NusDownloader(_client, BaseUrl).DownloadAsync(Reference.TitleId, _output));
    }

    [TestMethod]
    public async Task DownloadAsync_Cancelled_ThrowsOperationCanceledException()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => new NusDownloader(_client, BaseUrl).DownloadAsync(Reference.TitleId, _output, cancellationToken: source.Token));
    }

    [TestMethod]
    public void Constructor_BadArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new NusDownloader(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new NusDownloader(_client, "http://cdn.test/no-slash"));
        Assert.AreEqual(NusDownloader.DefaultBaseUrl, new NusDownloader(_client).BaseUrl);
        Assert.ThrowsExactlyAsync<ArgumentException>(() => new NusDownloader(_client).DownloadAsync(Reference.TitleId, "")).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Serves a packed folder the way the content server lays it out.
    /// </summary>
    private sealed class FakeCdn : HttpMessageHandler
    {
        private readonly string _package;
        private readonly string _prefix;

        public FakeCdn(string package, TitleId titleId)
        {
            _package = package;
            _prefix = BaseUrl + titleId.Value.ToString("x16") + "/";
        }

        public HttpStatusCode? FailWith { get; set; }
        public bool HasTicket { get; set; } = true;
        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = request.RequestUri!.ToString();
            Assert.IsTrue(url.StartsWith(_prefix, StringComparison.Ordinal), url);
            var name = url.Substring(_prefix.Length);
            Requests.Add(name);

            if (FailWith is not null)
                return Task.FromResult(new HttpResponseMessage(FailWith.Value));

            var file = name switch
            {
                "tmd" => NusFormat.TmdFileName,
                "cetk" => HasTicket ? NusFormat.TicketFileName : null,
                _ when name.EndsWith(".h3", StringComparison.Ordinal) => name,
                _ => name + ".app",
            };
            var path = file is null ? null : Path.Combine(_package, file);
            if (path is null || !File.Exists(path))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var bytes = File.ReadAllBytes(path);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.ContentLength = bytes.Length;
            return Task.FromResult(response);
        }
    }
}
