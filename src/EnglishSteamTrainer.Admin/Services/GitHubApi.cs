using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnglishSteamTrainer.Admin.Services;

public sealed class GitHubException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

public sealed record TreeEntry(string Path, string Type, string Sha);

/// <summary>
/// Zugriff auf das Repo über die GitHub-REST-API (Git-Daten-API). Gespeichert wird als ein
/// Commit mit allen geänderten Dateien; hat inzwischen jemand anderes auf den Branch
/// gepusht, schlägt das Speichern fehl statt fremde Änderungen zu überschreiben.
/// </summary>
public sealed class GitHubApi(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Repository { get; set; } = "";
    public string Token { get; set; } = "";

    public async Task<string> GetUserLoginAsync()
    {
        using var doc = await SendAsync(HttpMethod.Get, "user");
        return doc.RootElement.GetProperty("login").GetString() ?? "";
    }

    public async Task<string> GetBranchHeadAsync(string branch)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"repos/{Repository}/git/ref/heads/{branch}");
        return doc.RootElement.GetProperty("object").GetProperty("sha").GetString()!;
    }

    public async Task<string> GetCommitTreeAsync(string commitSha)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"repos/{Repository}/git/commits/{commitSha}");
        return doc.RootElement.GetProperty("tree").GetProperty("sha").GetString()!;
    }

    public async Task<List<TreeEntry>> GetTreeAsync(string treeSha)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"repos/{Repository}/git/trees/{treeSha}?recursive=1");

        return doc.RootElement.GetProperty("tree").EnumerateArray()
            .Select(entry => new TreeEntry(
                entry.GetProperty("path").GetString()!,
                entry.GetProperty("type").GetString()!,
                entry.GetProperty("sha").GetString()!))
            .ToList();
    }

    public async Task<string> GetBlobTextAsync(string blobSha)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"repos/{Repository}/git/blobs/{blobSha}");
        var base64 = doc.RootElement.GetProperty("content").GetString()!.Replace("\n", "");
        var text = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

        return text.TrimStart('﻿');
    }

    /// <summary>Ein Commit mit allen Dateien (Pfad relativ zum Repo), danach Branch vorspulen.</summary>
    public async Task<string> CommitAsync(string branch, string parentSha, IReadOnlyDictionary<string, string> files, string message)
    {
        var baseTree = await GetCommitTreeAsync(parentSha);
        var entries = new List<object>();

        foreach (var (path, text) in files)
        {
            using var blob = await SendAsync(HttpMethod.Post, $"repos/{Repository}/git/blobs", new { Content = text, Encoding = "utf-8" });
            entries.Add(new { Path = path, Mode = "100644", Type = "blob", Sha = blob.RootElement.GetProperty("sha").GetString() });
        }

        using var tree = await SendAsync(HttpMethod.Post, $"repos/{Repository}/git/trees", new { BaseTree = baseTree, Tree = entries });

        using var commit = await SendAsync(HttpMethod.Post, $"repos/{Repository}/git/commits", new
        {
            Message = message,
            Tree = tree.RootElement.GetProperty("sha").GetString(),
            Parents = new[] { parentSha }
        });

        var commitSha = commit.RootElement.GetProperty("sha").GetString()!;

        try
        {
            using var _ = await SendAsync(HttpMethod.Patch, $"repos/{Repository}/git/refs/heads/{branch}", new { Sha = commitSha, Force = false });
        }
        catch (GitHubException ex) when (ex.Status == HttpStatusCode.UnprocessableEntity)
        {
            throw new GitHubException(
                "Inzwischen hat jemand anderes etwas auf GitHub geändert. Bitte die Seite neu laden (deine Änderungen gehen dabei verloren) und noch einmal eintragen.",
                ex.Status);
        }

        return commitSha;
    }

    /// <summary>Markdown so darstellen wie GitHub (für die Berichte).</summary>
    public async Task<string> RenderMarkdownAsync(string markdown)
    {
        using var request = Request(HttpMethod.Post, "markdown", new { Text = markdown, Mode = "gfm", Context = Repository });
        using var response = await http.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var request = Request(method, path, body);
        using var response = await http.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private HttpRequestMessage Request(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, "https://api.github.com/" + path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        if (Token.Length > 0)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);

        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = "";

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            detail = doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? "" : "";
        }
        catch (JsonException)
        {
        }

        var text = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Token ungültig oder abgelaufen.",
            HttpStatusCode.Forbidden => "Kein Zugriff - hat das Token das Recht \"Contents: Read and write\" für dieses Repo?",
            HttpStatusCode.NotFound => "Nicht gefunden - Repo, Branch oder Token-Berechtigung prüfen.",
            _ => $"GitHub meldet HTTP {(int)response.StatusCode}."
        };

        throw new GitHubException(detail.Length > 0 ? $"{text} ({detail})" : text, response.StatusCode);
    }
}
