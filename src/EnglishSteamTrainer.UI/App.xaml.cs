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

    // Die Sprache legt die Installation fest (settings.json, nur für Admins änderbar).
    // "--language es" gilt nur für Konten, die dort nicht eingetragen sind, z. B. zum Testen.
    private static LearningLanguage GetLanguage(string[] args)
    {
        try
        {
            var profile = InstallSettings.Load().FindUser(DailyUnlock.CurrentUser);

            if (profile is not null)
                return profile.LearningLanguage;
        }
        catch
        {
            // Unlesbare settings.json - weiter mit Standard.
        }

        var index = Array.FindIndex(args, arg => arg is "--language" or "-language");

        return index >= 0 && index + 1 < args.Length
            ? Languages.Find(args[index + 1]) ?? Languages.English
            : Languages.English;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
