using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace EnglishSteamTrainer.Core;

/// <summary>
/// Richtige Antworten des heutigen Tages pro Windows-Konto. Einzige Quelle für
/// die Freischaltung: liegt unter ProgramData (damit der als SYSTEM laufende
/// Watchdog sie lesen kann) und ist signiert, damit ein Bearbeiten der Datei im
/// Editor nichts freischaltet. Fehlende, kaputte oder alte Dateien zählen als 0.
/// </summary>
public static class DailyUnlock
{
    // Kein echtes Geheimnis (steht im Programm), reicht aber gegen einfaches
    // Ändern der Datei von Hand.
    private static readonly byte[] SigningKey =
        Encoding.UTF8.GetBytes("EnglishSteamTrainer|daily-unlock|5b1e8c2f9d7a4e63");

    public static readonly string SharedFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "EnglishSteamTrainer");

    public static readonly string UnlockFolder = Path.Combine(SharedFolder, "unlocks");

    public static SecurityIdentifier CurrentUser => WindowsIdentity.GetCurrent().User!;

    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    public static int GetCorrectToday(SecurityIdentifier user)
    {
        try
        {
            var parts = File.ReadAllText(GetFile(user)).Trim().Split('|');

            if (parts.Length != 3
                || parts[0] != Today
                || !int.TryParse(parts[1], out var count))
                return 0;

            var expected = Encoding.ASCII.GetBytes(Sign(user, parts[0], count));
            var actual = Encoding.ASCII.GetBytes(parts[2]);

            return CryptographicOperations.FixedTimeEquals(expected, actual) ? count : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static bool IsUnlockedToday(SecurityIdentifier user, int requiredCorrectAnswers)
    {
        return GetCorrectToday(user) >= requiredCorrectAnswers;
    }

    public static void SetCorrectToday(int count)
    {
        var user = CurrentUser;
        var today = Today;
        var file = GetFile(user);
        var tempFile = file + ".tmp";

        Directory.CreateDirectory(UnlockFolder);

        // Erst temporär schreiben, dann ersetzen: der Watchdog sieht nie eine halbe Datei.
        File.WriteAllText(tempFile, $"{today}|{count}|{Sign(user, today, count)}");
        File.Move(tempFile, file, overwrite: true);
    }

    private static string GetFile(SecurityIdentifier user)
    {
        return Path.Combine(UnlockFolder, user.Value + ".txt");
    }

    private static string Sign(SecurityIdentifier user, string date, int count)
    {
        var data = Encoding.UTF8.GetBytes($"{user.Value}|{date}|{count}");
        return Convert.ToHexString(HMACSHA256.HashData(SigningKey, data));
    }
}
