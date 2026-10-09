using System.Net;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public sealed class ContentSyncTests : IDisposable
{
    private const string BaseUrl = "https://example.invalid/content/";

    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), "est-sync-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
            Directory.Delete(_cacheRoot, recursive: true);
    }

    private string Cached(string relativePath) => Path.Combine(_cacheRoot, "content", relativePath);

    private static string Bundled(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", relativePath));

    private Task<ContentSyncResult> Sync(Func<string, HttpResponseMessage> respond)
    {
        var http = new HttpClient(new FakeHandler(respond));
        return ContentSync.SyncAsync(http, BaseUrl, _cacheRoot);
    }

    private static HttpResponseMessage Ok(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };

    [Fact]
    public async Task Valid_files_are_cached()
    {
        var result = await Sync(path => Ok(Bundled(path)));

        Assert.Empty(result.Errors);
        Assert.Equal(ContentStore.AllFiles.Count(), result.Updated.Count);
        Assert.Equal(Bundled("es/vocabulary.csv"), File.ReadAllText(Cached("es/vocabulary.csv")));
        Assert.True(File.Exists(Path.Combine(_cacheRoot, "last-sync.txt")));

        var second = await Sync(path => Ok(Bundled(path)));
        Assert.Empty(second.Updated);
    }

    [Fact]
    public async Task Broken_online_config_does_not_replace_last_good_version()
    {
        await Sync(path => Ok(Bundled(path)));

        var result = await Sync(path => path == ContentStore.ConfigFile
            ? Ok("""{ "blockedApps": [] }""")
            : Ok(Bundled(path)));

        Assert.Single(result.Errors);
        Assert.Equal(Bundled(ContentStore.ConfigFile), File.ReadAllText(Cached(ContentStore.ConfigFile)));
    }

    [Fact]
    public async Task Offline_keeps_cache_and_reports_it()
    {
        await Sync(path => Ok(Bundled(path)));

        var result = await Sync(_ => throw new HttpRequestException("Kein Netz"));

        Assert.False(result.ReachedServer);
        Assert.Equal(ContentStore.AllFiles.Count(), result.Errors.Count);
        Assert.True(File.Exists(Cached("en/vocabulary.csv")));
    }

    [Fact]
    public async Task Missing_file_on_server_is_an_error_not_an_empty_list()
    {
        var result = await Sync(path => path.StartsWith("es/")
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : Ok(Bundled(path)));

        Assert.Equal(2, result.Errors.Count);
        Assert.False(File.Exists(Cached("es/vocabulary.csv")));
    }

    private sealed class FakeHandler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var relativePath = request.RequestUri!.AbsolutePath["/content/".Length..];
            return Task.FromResult(respond(relativePath));
        }
    }
}
