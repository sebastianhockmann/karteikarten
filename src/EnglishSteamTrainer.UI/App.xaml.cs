using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.UI;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private string? _testJournalFile;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Nicht gesperrte Konten (Entwickler-PC, Eltern) bekommen den Entwicklermodus -
        // sonst würde der Ersatz-Watchdog Browser & Co. des eigenen Kontos beenden.
        var blocked = TestMode.IsBlockedAccount(DailyUnlock.CurrentUser);

        if (!blocked && !TestMode.IsChildModeRequested(e.Args))
        {
            StartTestMode(TestMode.Parse(e.Args));
            return;
        }

        if (blocked && TestMode.IsRequested(e.Args))
        {
            MessageBox.Show(
                "Der Entwicklermodus ist für gesperrte Konten nicht verfügbar.",
                "Entwicklermodus",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Nur eine Instanz pro Konto - sonst würden zwei Fenster in dasselbe
        // Lernjournal schreiben (z. B. Autostart plus Doppelklick).
        _singleInstance = new Mutex(true, @"Local\EnglishSteamTrainer.UI", out var isFirstInstance);

        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        var user = DailyUnlock.CurrentUser;
        var journal = AnswerJournal.Load(user);
        LegacyProgress.ImportInto(journal);

        var window = new MainWindow(GetLanguage(e.Args), journal, simulatedChild: !blocked);
        MainWindow = window;
        window.Show();
    }

    // Eigenes Wegwerf-Journal statt des echten, damit Testantworten weder Punkte
    // noch den Verlauf verändern. Wird beim Beenden gelöscht.
    private void StartTestMode(TestOptions options)
    {
        _testJournalFile = Path.Combine(Path.GetTempPath(), $"EnglishSteamTrainer-test-{Guid.NewGuid():N}.journal.txt");
        var journal = AnswerJournal.Load(DailyUnlock.CurrentUser, _testJournalFile);

        var language = options.Language is null
            ? GetLanguage([])
            : new AssignedLanguage(options.Language, LanguageSource.Default);

        var window = new MainWindow(language, journal, options);
        MainWindow = window;
        window.Show();
    }

    // Sprache: zentral aus content/config.json, sonst aus der Installation (settings.json) -
    // beides kann das Kind nicht ändern. "--language es" gilt nur für Konten, die nirgends
    // eingetragen sind, z. B. zum Testen.
    private static AssignedLanguage GetLanguage(string[] args)
    {
        try
        {
            var assigned = LanguageAssignment.Resolve(
                ContentStore.LoadConfig().Value,
                InstallSettings.Load(),
                DailyUnlock.CurrentUser,
                Environment.MachineName,
                Environment.UserName);

            if (assigned is not null)
                return assigned;
        }
        catch
        {
            // Unlesbare settings.json - weiter mit Standard.
        }

        var index = Array.FindIndex(args, arg => arg is "--language" or "-language");
        var language = index >= 0 && index + 1 < args.Length ? Languages.Find(args[index + 1]) : null;

        return new AssignedLanguage(language ?? Languages.English, LanguageSource.Default);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();

        try
        {
            if (_testJournalFile is not null)
                File.Delete(_testJournalFile);
        }
        catch
        {
        }

        base.OnExit(e);
    }
}
