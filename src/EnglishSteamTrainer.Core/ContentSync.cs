using System.Text;

namespace EnglishSteamTrainer.Core;

public sealed record ContentSyncResult(List<string> Updated, List<string> Errors, bool ReachedServer);

/// <summary>
/// Lädt content/ aus dem GitHub-Repo in den Cache. Läuft im Watchdog als SYSTEM -
/// nur dann ist der Cache-Ordner beschreibbar, das Kind kann ihn nicht verändern.
/// </summary>
public static class ContentSync
{
    public static async Task<ContentSyncResult> SyncAsync(
        HttpClient http,
        string baseUrl,
        string? cacheRoot = null,
        CancellationToken cancellationToken = default)
    {
        var cacheFolder = cacheRoot is null ? ContentStore.CacheFolder : Path.Combine(cacheRoot, "content");
        var lastSyncFile = cacheRoot is null ? ContentStore.LastSyncFile : Path.Combine(cacheRoot, "last-sync.txt");

        var updated = new List<string>();
        var errors = new List<string>();
        var reachedServer = false;
        var baseUri = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

        Directory.CreateDirectory(cacheFolder);

        foreach (var relativePath in ContentStore.AllFiles)
        {
            try
            {
                using var response = await http.GetAsync(new Uri(baseUri, relativePath), cancellationToken);
                reachedServer = true;

                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"{relativePath}: HTTP {(int)response.StatusCode}");
                    continue;
                }

                var text = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!ContentStore.IsValid(relativePath, text))
                {
                    errors.Add($"{relativePath}: ungültiger Inhalt, bisherige Version bleibt aktiv");
                    continue;
                }

                var target = Path.Combine(cacheFolder, relativePath);

                if (File.Exists(target) && File.ReadAllText(target) == text)
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                var tempFile = target + ".tmp";
                File.WriteAllText(tempFile, text, new UTF8Encoding(false));
                File.Move(tempFile, target, overwrite: true);

                updated.Add(relativePath);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                errors.Add($"{relativePath}: {ex.Message}");
            }
        }

        if (reachedServer)
            File.WriteAllText(lastSyncFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return new ContentSyncResult(updated, errors, reachedServer);
    }
}
