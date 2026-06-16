using System;
using System.Windows;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var progress = ProgressStore.Load();
        progress.CorrectToday = 0;
        progress.WrongToday = 0;
        progress.SteamUnlockedToday = false;
        ProgressStore.Save(progress);
    }
}