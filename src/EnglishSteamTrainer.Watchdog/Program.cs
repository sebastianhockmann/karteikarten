using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using EnglishSteamTrainer.Core;

// Läuft im Normalbetrieb als SYSTEM (Aufgabenplanung, siehe scripts\Install.ps1):
// dann kann das Kind ihn nicht beenden, gesperrt werden die Konten aus settings.json,
// und er hält die Online-Inhalte (Vokabeln, Sperrliste) im Cache aktuell.
// Von der UI gestartet (Entwicklung/Ersatz) sperrt er nur das eigene Konto.

var pollInterval = TimeSpan.FromSeconds(1);
var configReloadInterval = TimeSpan.FromSeconds(15);
var syncInterval = TimeSpan.FromMinutes(30);
var maxLogSize = 1024 * 1024;

using var identity = WindowsIdentity.GetCurrent();
var currentUser = identity.User!;
var runsAsSystem = identity.IsSystem;

// Ein Watchdog pro Konto - der SYSTEM-Watchdog und ein Ersatz-Watchdog im
// Kind-Konto behindern sich so nicht gegenseitig.
using var mutex = new Mutex(
    true,
    $@"Global\EnglishSteamTrainer.Watchdog.{currentUser.Value}",
    out var isFirstInstance);

if (!isFirstInstance)
    return;

var logFolder = Path.Combine(DailyUnlock.SharedFolder, "logs");
var logFile = Path.Combine(logFolder, $"watchdog-{Environment.UserName}.log");
var logLock = new object();

var blockedUsers = new List<(SecurityIdentifier Sid, string Account)>();
var blockedUsersDescription = "";
var settingsFileTime = DateTime.MinValue;
var usersLoadedAt = DateTime.MinValue;

var config = ContentStore.LoadConfig();
var configLoadedAt = DateTime.UtcNow;

Log(runsAsSystem
    ? $"Watchdog {AppVersion.Current} gestartet (SYSTEM, Konten aus {InstallSettings.FilePath})."
    : $"Watchdog {AppVersion.Current} gestartet (nur Konto {Environment.UserName}).");
LogConfig();

if (runsAsSystem)
    _ = Task.Run(SyncContentLoop);

while (true)
{
    try
    {
        if (DateTime.UtcNow - configLoadedAt > configReloadInterval)
        {
            var reloaded = ContentStore.LoadConfig();
            configLoadedAt = DateTime.UtcNow;

            if (Describe(reloaded) != Describe(config))
            {
                config = reloaded;
                LogConfig();
            }
        }

        // Anzahl pro Konto (content/config.json -> users), sonst der allgemeine Wert.
        var lockedUsers = GetBlockedUsers()
            .Where(user => !DailyUnlock.IsUnlockedToday(
                user.Sid,
                config.Value.RequiredCorrectAnswersFor(Environment.MachineName, user.Account)))
            .Select(user => user.Sid)
            .ToList();

        var killed = AppBlocker.KillBlockedProcesses(lockedUsers, config.Value.BlockedApps);

        if (killed.Count > 0)
            Log("Beendet: " + string.Join(", ", killed));
    }
    catch (Exception ex)
    {
        Log("Fehler: " + ex.Message);
    }

    Thread.Sleep(pollInterval);
}

async Task SyncContentLoop()
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd($"EnglishSteamTrainer/{AppVersion.Current}");

    var lastErrors = "";

    while (true)
    {
        try
        {
            var settings = InstallSettings.Load();
            var result = await ContentSync.SyncAsync(http, settings.ContentBaseUrl);

            if (result.Updated.Count > 0)
                Log("Online-Inhalte aktualisiert: " + string.Join(", ", result.Updated));

            // Fehler nur bei Änderung loggen - sonst füllt ein Offline-Tag das Log.
            var errors = string.Join("; ", result.Errors);

            if (errors != lastErrors && errors.Length > 0)
                Log("Online-Inhalte: " + errors + (result.ReachedServer ? "" : " (offline - Cache bleibt aktiv)"));

            lastErrors = errors;
        }
        catch (Exception ex)
        {
            Log("Fehler beim Abgleich der Online-Inhalte: " + ex.Message);
        }

        await Task.Delay(syncInterval);
    }
}

List<(SecurityIdentifier Sid, string Account)> GetBlockedUsers()
{
    if (!runsAsSystem)
        return [(currentUser, Environment.UserName)];

    var fileTime = File.Exists(InstallSettings.FilePath)
        ? File.GetLastWriteTimeUtc(InstallSettings.FilePath)
        : DateTime.MinValue;

    // Bei Änderung sofort, sonst jede Minute (z. B. falls ein Konto erst später angelegt wird).
    if (fileTime == settingsFileTime && DateTime.UtcNow - usersLoadedAt < TimeSpan.FromMinutes(1))
        return blockedUsers;

    // Erst komplett einlesen, dann übernehmen: schlägt das Lesen fehl, bleibt die
    // bisherige Liste aktiv und es wird im nächsten Durchlauf erneut versucht.
    var settings = InstallSettings.Load();
    var users = new List<(SecurityIdentifier Sid, string Account)>();
    var names = new List<string>();

    foreach (var profile in settings.Users)
    {
        var sid = profile.TryGetSid();

        if (sid is null)
        {
            names.Add($"{profile.Account} (Konto nicht gefunden!)");
            continue;
        }

        users.Add((sid, profile.Account));
        var central = config.Value.FindLanguage(Environment.MachineName, profile.Account);
        var required = config.Value.RequiredCorrectAnswersFor(Environment.MachineName, profile.Account);
        names.Add(central is null
            ? $"{profile.Account} ({profile.LearningLanguage.Name} lokal, {required} Antworten)"
            : $"{profile.Account} ({central.Name} zentral, {required} Antworten)");
    }

    var description = names.Count == 0
        ? $"Warnung: keine Konten in {InstallSettings.FilePath} - es wird niemand gesperrt."
        : "Gesperrte Konten: " + string.Join(", ", names);

    if (description != blockedUsersDescription)
        Log(description);

    settingsFileTime = fileTime;
    usersLoadedAt = DateTime.UtcNow;
    blockedUsers = users;
    blockedUsersDescription = description;

    return blockedUsers;
}

string Describe(LoadedContent<TrainerConfig> loaded)
{
    return $"{loaded.Source}: {loaded.Value.RequiredCorrectAnswers} Antworten, " +
           string.Join(", ", loaded.Value.BlockedApps.Select(app => $"{app.Name} [{string.Join(" ", app.Executables)}]"));
}

void LogConfig()
{
    Log("Konfiguration: " + Describe(config));
}

void Log(string message)
{
    lock (logLock)
    {
        try
        {
            Directory.CreateDirectory(logFolder);

            if (File.Exists(logFile) && new FileInfo(logFile).Length > maxLogSize)
                File.Move(logFile, logFile + ".old", overwrite: true);

            File.AppendAllText(
                logFile,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging darf den Watchdog nie stoppen.
        }
    }
}
