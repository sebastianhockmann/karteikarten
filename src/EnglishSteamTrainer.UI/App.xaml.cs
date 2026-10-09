using System;
using System.Linq;
using System.Threading;
using System.Windows;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.UI;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        var window = new MainWindow(GetLanguage(e.Args), journal);
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
        base.OnExit(e);
    }
}
