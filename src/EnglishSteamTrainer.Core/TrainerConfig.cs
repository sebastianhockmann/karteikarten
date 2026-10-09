using System.Text.Json;

namespace EnglishSteamTrainer.Core;

public sealed record BlockedApp(string Name, string[] Executables);

/// <param name="Computer">PC-Name (wie "hostname"), leer oder "*" = jeder PC.</param>
/// <param name="Language">Optional, sonst gilt die Sprache aus der Installation.</param>
/// <param name="RequiredCorrectAnswers">Optional, sonst gilt der allgemeine Wert.</param>
public sealed record UserSettings(
    string? Computer,
    string Account,
    string? Language = null,
    int? RequiredCorrectAnswers = null);

/// <summary>
/// Zentrale Einstellungen für alle PCs (content/config.json im GitHub-Repo).
/// </summary>
public sealed class TrainerConfig
{
    public int RequiredCorrectAnswers { get; set; } = 15;
    public int GrammarQuestionsPerSession { get; set; } = 5;
    public List<BlockedApp> BlockedApps { get; set; } = [];

    /// <summary>
    /// Zentrale Einstellungen pro Konto (Sprache, Anzahl Aufgaben). Haben Vorrang vor
    /// der lokalen settings.json, legen aber nicht fest, wer gesperrt ist - das
    /// bestimmt weiterhin die Installation auf dem PC (Install.ps1).
    /// </summary>
    public List<UserSettings> Users { get; set; } = [];

    public LearningLanguage? FindLanguage(string computer, string account)
    {
        var code = FindUserValue(computer, account, user => user.Language);
        return code is null ? null : Languages.Find(code);
    }

    public int RequiredCorrectAnswersFor(string computer, string account)
    {
        return FindUserValue(computer, account, user => user.RequiredCorrectAnswers) ?? RequiredCorrectAnswers;
    }

    // Pro Einstellung: Eintrag für genau diesen PC vor Eintrag für alle PCs. So kann
    // z. B. "*" die Sprache festlegen und ein PC-Eintrag nur die Anzahl ändern.
    private T? FindUserValue<T>(string computer, string account, Func<UserSettings, T?> select)
    {
        var matches = Users
            .Where(user => string.Equals(user.Account, account, StringComparison.OrdinalIgnoreCase)
                           && select(user) is not null)
            .ToList();

        var match = matches.FirstOrDefault(user => string.Equals(user.Computer?.Trim(), computer, StringComparison.OrdinalIgnoreCase))
                    ?? matches.FirstOrDefault(user => string.IsNullOrWhiteSpace(user.Computer) || user.Computer.Trim() == "*");

        return match is null ? default : select(match);
    }

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

            // Unbekannte Sprache oder fehlender Kontoname: ganze Datei ablehnen, damit
            // ein Tippfehler auffällt (CI-Test) statt still ignoriert zu werden.
            config.Users ??= [];

            if (config.Users.Any(user =>
                    string.IsNullOrWhiteSpace(user.Account)
                    || user.Language is not null && Languages.Find(user.Language) is null
                    || user.RequiredCorrectAnswers is < 1 or > 200))
                return null;

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
