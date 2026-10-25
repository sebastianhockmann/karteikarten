using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnglishSteamTrainer.Core;

/// <summary>
/// Ziel für die Fortschrittsberichte: Repo, Branch und ein Token mit Schreibrecht.
/// Liegt in report-upload.json im Programmordner - anders als settings.json nur für
/// SYSTEM und Admins lesbar (scripts\Install.ps1 -ReportToken).
/// </summary>
public sealed class ReportUploadSettings
{
    // Eigener Branch, damit die Berichte im Programm-Repo weder main noch die CI berühren.
    public const string DefaultBranch = "berichte";

    public string Repository { get; set; } = "";
    public string Branch { get; set; } = DefaultBranch;
    public string Token { get; set; } = "";

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "report-upload.json");

    /// <summary>Null, wenn keine Berichte hochgeladen werden sollen.</summary>
    public static ReportUploadSettings? Load()
    {
        if (!File.Exists(FilePath))
            return null;

        var settings = JsonSerializer.Deserialize<ReportUploadSettings>(File.ReadAllText(FilePath), InstallSettings.JsonOptions);

        if (string.IsNullOrWhiteSpace(settings?.Repository) || string.IsNullOrWhiteSpace(settings.Token))
            return null;

        if (string.IsNullOrWhiteSpace(settings.Branch))
            settings.Branch = DefaultBranch;

        return settings;
    }
}

/// <summary>Schreibt eine Datei über die GitHub-API in den Berichte-Branch.</summary>
public static class ReportUpload
{
    /// <returns>false, wenn die Datei dort schon genau so liegt (dann kein neuer Commit).</returns>
    public static async Task<bool> UploadAsync(
        HttpClient http,
        ReportUploadSettings settings,
        string path,
        string content,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(
            $"https://api.github.com/repos/{settings.Repository.Trim()}/contents/" +
            string.Join('/', path.Split('/').Select(Uri.EscapeDataString)));
        var branch = settings.Branch.Trim();

        string? sha = null;

        using (var request = CreateRequest(HttpMethod.Get, new Uri($"{uri}?ref={Uri.EscapeDataString(branch)}"), settings.Token))
        using (var response = await http.SendAsync(request, cancellationToken))
        {
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                await EnsureSuccessAsync(response, settings, cancellationToken);

                using var existing = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                sha = existing.RootElement.GetProperty("sha").GetString();
                var existingContent = existing.RootElement.GetProperty("content").GetString() ?? "";

                if (Encoding.UTF8.GetString(Convert.FromBase64String(existingContent.Replace("\n", ""))) == content)
                    return false;
            }
        }

        var body = JsonSerializer.Serialize(new
        {
            message = commitMessage,
            content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            branch,
            sha
        }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

        using (var request = CreateRequest(HttpMethod.Put, uri, settings.Token))
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, settings, cancellationToken);
        }

        return true;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        ReportUploadSettings settings,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var hint = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Token ungültig oder abgelaufen",
            HttpStatusCode.Forbidden or HttpStatusCode.NotFound =>
                $"kein Schreibzugriff auf {settings.Repository}, Branch {settings.Branch} (Branch angelegt? Token: Repo ausgewählt? Recht \"Contents: Read and write\"?)",
            _ => await response.Content.ReadAsStringAsync(cancellationToken)
        };

        throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {hint}");
    }
}
