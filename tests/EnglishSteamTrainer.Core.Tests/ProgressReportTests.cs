using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public sealed class ProgressReportTests : IDisposable
{
    private static readonly SecurityIdentifier User = new("S-1-5-21-1000-2000-3000-1001");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    private static readonly ReportUploadSettings Target = new()
    {
        Repository = "eltern/karteikarten",
        Token = "github_pat_test"
    };

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "est-report-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private AnswerJournal Journal(params AnswerResult[] results)
    {
        var journal = AnswerJournal.Load(User, Path.Combine(_folder, "journal.txt"));

        for (var i = 0; i < results.Length; i++)
            journal.RecordAnswer(Languages.Spanish, JournalKind.Vocabulary, $"word{i}", $"word{i}", "x", results[i]);

        return journal;
    }

    [Fact]
    public void Days_without_answers_are_listed_with_zero()
    {
        var days = ProgressReport.GetDays(Journal(AnswerResult.Correct, AnswerResult.Typo, AnswerResult.Wrong), 2, Today);

        Assert.Equal(ProgressReport.Days, days.Count);
        Assert.Equal(Today, days[^1].Date);
        Assert.Equal(new ProgressDay(Today, 2, 1, true), days[^1]);
        Assert.All(days[..^1], day => Assert.False(day.Learned));
    }

    [Fact]
    public void Goal_is_not_reached_below_required_answers()
    {
        var days = ProgressReport.GetDays(Journal(AnswerResult.Correct, AnswerResult.Wrong), 15, Today);

        Assert.False(days[^1].GoalReached);
        Assert.True(days[^1].Learned);
    }

    [Fact]
    public void Markdown_contains_chart_and_table()
    {
        var markdown = ProgressReport.BuildMarkdown("GROOT", "leand", Languages.Spanish, 1, Journal(AnswerResult.Correct), Today);

        Assert.Contains("# Lernfortschritt leand (GROOT)", markdown);
        Assert.Contains("```mermaid", markdown);
        Assert.Contains("xychart-beta", markdown);
        Assert.Contains("an 1 Tagen gelernt, Tagesziel 1× erreicht", markdown);
        Assert.Contains("| 1 | 0 | ✅ |", markdown);
        Assert.Contains("– nicht gelernt", markdown);
    }

    [Fact]
    public async Task New_report_is_created_without_sha()
    {
        var api = new FakeGitHub();

        Assert.True(await ReportUpload.UploadAsync(api.Client, Target, "GROOT/leand.md", "Bericht", "Test"));

        var put = Assert.Single(api.Puts);
        Assert.Equal("/repos/eltern/karteikarten/contents/GROOT/leand.md", put.Path);
        Assert.Equal("berichte", put.Body.RootElement.GetProperty("branch").GetString());
        Assert.False(put.Body.RootElement.TryGetProperty("sha", out _));
        Assert.Equal("Bericht", Decode(put.Body.RootElement.GetProperty("content").GetString()!));
    }

    [Fact]
    public async Task Unchanged_report_is_not_uploaded_again()
    {
        var api = new FakeGitHub { Existing = "Bericht" };

        Assert.False(await ReportUpload.UploadAsync(api.Client, Target, "GROOT/leand.md", "Bericht", "Test"));
        Assert.Empty(api.Puts);
    }

    [Fact]
    public async Task Changed_report_replaces_existing_file()
    {
        var api = new FakeGitHub { Existing = "alt" };

        Assert.True(await ReportUpload.UploadAsync(api.Client, Target, "GROOT/leand.md", "neu", "Test"));
        Assert.Equal("abc123", Assert.Single(api.Puts).Body.RootElement.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task Expired_token_gives_a_clear_error()
    {
        var api = new FakeGitHub { Status = HttpStatusCode.Unauthorized };

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => ReportUpload.UploadAsync(api.Client, Target, "GROOT/leand.md", "neu", "Test"));

        Assert.Contains("abgelaufen", error.Message);
    }

    private static string Decode(string base64) => Encoding.UTF8.GetString(Convert.FromBase64String(base64));

    private sealed class FakeGitHub : HttpMessageHandler
    {
        public string? Existing { get; init; }
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public List<(string Path, JsonDocument Body)> Puts { get; } = [];
        public HttpClient Client => new(this);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer github_pat_test", request.Headers.Authorization?.ToString());

            if (Status != HttpStatusCode.OK)
                return new HttpResponseMessage(Status);

            if (request.Method == HttpMethod.Put)
            {
                Puts.Add((request.RequestUri!.AbsolutePath, JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))));
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            Assert.Equal("?ref=berichte", request.RequestUri!.Query);

            if (Existing is null)
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            // GitHub bricht Base64 alle 60 Zeichen mit \n um.
            var content = Convert.ToBase64String(Encoding.UTF8.GetBytes(Existing)).Insert(2, "\n");
            var json = JsonSerializer.Serialize(new { sha = "abc123", content });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }
}
