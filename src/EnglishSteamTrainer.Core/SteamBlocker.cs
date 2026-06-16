using System.Diagnostics;

namespace EnglishSteamTrainer.Core;

public static class SteamBlocker
{
    private static readonly string[] SteamProcesses =
    [
        "steam",
        "steamwebhelper",
        "GameOverlayUI"
    ];

    public static bool IsUnlocked()
    {
        var progress = ProgressStore.Load();
        return progress.SteamUnlockedToday;
    }

    public static int KillSteamIfLocked()
    {
        if (IsUnlocked())
            return 0;

        var killed = 0;
        foreach (var name in SteamProcesses)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    killed++;
                }
                catch
                {
                    // Keine Adminrechte oder Prozess schon beendet.
                }
            }
        }
        return killed;
    }

    public static void StartSteam()
    {
        if (!IsUnlocked())
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "steam://open/main",
                UseShellExecute = true
            });
        }
        catch
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam",
                "steam.exe");

            if (File.Exists(path))
                Process.Start(path);
        }
    }
}
