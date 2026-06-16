using System;
using System.IO;
using System.Threading;
using EnglishSteamTrainer.Core;

var appFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "EnglishSteamTrainer");

Directory.CreateDirectory(appFolder);

var logFile = Path.Combine(appFolder, "watchdog.log");

File.AppendAllText(
    logFile,
    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Watchdog gestartet.{Environment.NewLine}");

while (true)
{
    try
    {
        var unlocked = SteamBlocker.IsUnlocked();
        var killed = SteamBlocker.KillSteamIfLocked();

        File.AppendAllText(
            logFile,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: SteamUnlocked={unlocked}, Killed={killed}{Environment.NewLine}");
    }
    catch (Exception ex)
    {
        File.AppendAllText(
            logFile,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Fehler: {ex.Message}{Environment.NewLine}");
    }

    Thread.Sleep(TimeSpan.FromSeconds(2));
}