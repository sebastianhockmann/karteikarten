using System.Text.Json;

namespace EnglishSteamTrainer.Core;

/// <summary>
/// Frühere Versionen speicherten XP frei änderbar in %LOCALAPPDATA%\EnglishSteamTrainer\progress.json.
/// Der Stand wird einmalig als sichtbarer "Übernahme"-Eintrag ins Lernjournal übernommen.
/// </summary>
public static class LegacyProgress
{
    private static readonly string ProgressFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EnglishSteamTrainer",
        "progress.json");

    public static void ImportInto(AnswerJournal journal)
    {
        try
        {
            if (!File.Exists(ProgressFile))
                return;

            if (journal.Entries.Count == 0)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(ProgressFile));

                if (document.RootElement.TryGetProperty("Xp", out var xpElement)
                    && xpElement.TryGetInt32(out var xp)
                    && xp > 0)
                {
                    journal.RecordImport("Punktestand aus der alten Version übernommen", xp);
                }
            }

            File.Move(ProgressFile, ProgressFile + ".uebernommen", overwrite: true);
        }
        catch
        {
            // Beim nächsten Start erneut versuchen.
        }
    }
}
