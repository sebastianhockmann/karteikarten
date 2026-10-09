using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace EnglishSteamTrainer.Core;

public static class AppBlocker
{
    // Pfad -> Originaldateiname aus den Versionsinfos. Erkennt auch umbenannte
    // Kopien (z. B. steam.exe -> spiel.exe), ohne jede Sekunde die Datei zu lesen.
    private static readonly Dictionary<string, string?> OriginalNameCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Beendet alle gesperrten Programme, die einem der angegebenen Konten gehören.
    /// Prozesse anderer Konten (z. B. Eltern) bleiben unberührt.
    /// </summary>
    public static List<string> KillBlockedProcesses(
        IReadOnlyCollection<SecurityIdentifier> lockedUsers,
        IEnumerable<BlockedApp> blockedApps)
    {
        var killed = new List<string>();

        if (lockedUsers.Count == 0)
            return killed;

        var blockedExecutables = new HashSet<string>(
            blockedApps.SelectMany(app => app.Executables),
            StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (!TryGetOwnerAndPath(process.Id, out var owner, out var imagePath)
                        || !lockedUsers.Contains(owner))
                        continue;

                    if (!IsBlocked(process.ProcessName, imagePath, blockedExecutables))
                        continue;

                    process.Kill(entireProcessTree: true);
                    killed.Add($"{process.ProcessName} ({process.Id})");
                }
                catch
                {
                    // Prozess bereits beendet oder kein Zugriff - im nächsten Durchlauf erneut.
                }
            }
        }

        return killed;
    }

    public static void StartSteam(int requiredCorrectAnswers)
    {
        if (!DailyUnlock.IsUnlockedToday(DailyUnlock.CurrentUser, requiredCorrectAnswers))
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

    private static bool IsBlocked(string processName, string? imagePath, HashSet<string> blockedExecutables)
    {
        if (blockedExecutables.Contains(processName + ".exe"))
            return true;

        if (string.IsNullOrEmpty(imagePath))
            return false;

        if (!OriginalNameCache.TryGetValue(imagePath, out var originalName))
        {
            try
            {
                originalName = FileVersionInfo.GetVersionInfo(imagePath).OriginalFilename;
            }
            catch
            {
                originalName = null;
            }

            if (OriginalNameCache.Count > 5000)
                OriginalNameCache.Clear();

            OriginalNameCache[imagePath] = originalName;
        }

        return originalName is not null && blockedExecutables.Contains(originalName);
    }

    private static bool TryGetOwnerAndPath(
        int processId,
        out SecurityIdentifier owner,
        out string? imagePath)
    {
        owner = null!;
        imagePath = null;

        var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);

        if (processHandle == IntPtr.Zero)
            return false;

        try
        {
            if (!OpenProcessToken(processHandle, TokenQuery, out var tokenHandle))
                return false;

            try
            {
                using var identity = new WindowsIdentity(tokenHandle);

                if (identity.User is null)
                    return false;

                owner = identity.User;
            }
            finally
            {
                CloseHandle(tokenHandle);
            }

            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;

            if (QueryFullProcessImageName(processHandle, 0, buffer, ref size))
                imagePath = buffer.ToString(0, size);

            return true;
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr processHandle, int flags, StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
