using System.Text.Json;

namespace EnglishSteamTrainer.Core;

public sealed record BlockedApp(string Name, string[] Executables);

/// <summary>
/// Zentrale Einstellungen für alle PCs (content/config.json im GitHub-Repo).
/// </summary>
public sealed class TrainerConfig
{
    public int RequiredCorrectAnswers { get; set; } = 15;
    public int GrammarQuestionsPerSession { get; set; } = 5;
    public List<BlockedApp> BlockedApps { get; set; } = [];

    // Greift nur, wenn weder Online-Cache noch mitgelieferte config.json lesbar sind.
    public static TrainerConfig Default => new()
    {
        BlockedApps =
        [
            new("Steam", ["steam.exe", "steamwebhelper.exe", "GameOverlayUI.exe"]),
            new("Chrome", ["chrome.exe"]),
            new("Edge", ["msedge.exe"]),
            new("Discord", ["Discord.exe", "DiscordPTB.exe", "DiscordCanary.exe"])
        ]
    };

    public string BlockedAppsText
    {
        get
        {
            var names = BlockedApps.Select(app => app.Name).ToList();

            return names.Count <= 1
                ? string.Join("", names)
                : string.Join(", ", names[..^1]) + " und " + names[^1];
        }
    }

    /// <summary>
    /// Liefert null, wenn die Datei unbrauchbar ist. Eine leere Sperrliste gilt
    /// absichtlich als Fehler: ein kaputter Upload darf nicht alles freischalten.
    /// </summary>
    public static TrainerConfig? TryParse(string json)
    {
        try
        {
            var config = JsonSerializer.Deserialize<TrainerConfig>(json, InstallSettings.JsonOptions);

            if (config?.BlockedApps is null)
                return null;

            config.BlockedApps = config.BlockedApps
                .Where(app => !string.IsNullOrWhiteSpace(app.Name) && app.Executables is not null)
                .Select(app => app with
                {
                    Name = app.Name.Trim(),
                    Executables = app.Executables
                        .Where(exe => !string.IsNullOrWhiteSpace(exe))
                        .Select(exe => exe.Trim())
                        .Select(exe => exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe : exe + ".exe")
                        .ToArray()
                })
                .Where(app => app.Executables.Length > 0)
                .ToList();

            if (config.BlockedApps.Count == 0
                || config.RequiredCorrectAnswers is < 1 or > 200
                || config.GrammarQuestionsPerSession is < 0 or > 100)
                return null;

            return config;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
