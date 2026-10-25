using System.Globalization;
using System.Text;

namespace EnglishSteamTrainer.Core;

public sealed record ProgressDay(DateOnly Date, int Correct, int Wrong, bool GoalReached)
{
    public bool Learned => Correct + Wrong > 0;
}

/// <summary>
/// Fortschrittsbericht für die Eltern: richtige Antworten pro Tag der letzten Wochen als
/// Grafik (Mermaid, zeigt GitHub direkt an) und Tabelle. Tage ohne Antwort erscheinen
/// ausdrücklich mit 0, damit man sieht, wann nicht gelernt wurde.
/// </summary>
public static class ProgressReport
{
    public const int Days = 30;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static List<ProgressDay> GetDays(AnswerJournal journal, int requiredCorrectAnswers, DateOnly today, int days = Days)
    {
        var summaries = journal.GetDaySummaries().ToDictionary(summary => summary.Date);

        return Enumerable.Range(0, days)
            .Select(offset => today.AddDays(offset - days + 1))
            .Select(date => summaries.TryGetValue(date, out var summary)
                ? new ProgressDay(
                    date,
                    summary.Correct + summary.Typos,
                    summary.Wrong,
                    summary.Correct + summary.Typos >= requiredCorrectAnswers)
                : new ProgressDay(date, 0, 0, false))
            .ToList();
    }

    /// <summary>
    /// Enthält bewusst keine Uhrzeit des Erstellens, nur das Datum: so ändert sich der
    /// Bericht (und damit der Upload) nur bei neuen Antworten oder einmal am Tag.
    /// </summary>
    public static string BuildMarkdown(
        string computer,
        string account,
        LearningLanguage language,
        int requiredCorrectAnswers,
        AnswerJournal journal,
        DateOnly today)
    {
        var days = GetDays(journal, requiredCorrectAnswers, today);
        var lastAnswer = journal.Entries.LastOrDefault(entry => entry.Result is not null);
        var text = new StringBuilder();

        text.AppendLine($"# Lernfortschritt {account} ({computer})");
        text.AppendLine();
        text.AppendLine($"{language.Name} · Tagesziel {requiredCorrectAnswers} richtige Antworten · Stand {today.ToString("dd.MM.yyyy", German)}");
        text.AppendLine();
        text.AppendLine(
            $"**Letzte {Days} Tage:** an {days.Count(day => day.Learned)} Tagen gelernt, " +
            $"Tagesziel {days.Count(day => day.GoalReached)}× erreicht · " +
            $"zuletzt gelernt: {(lastAnswer is null ? "noch nie" : lastAnswer.Time.ToString("dd.MM.yyyy HH:mm", German))} · " +
            $"insgesamt {journal.TotalXp} XP");
        text.AppendLine();

        if (journal.IntegrityProblem is not null)
        {
            text.AppendLine($"> ⚠️ Das Lernjournal wurde verändert: {journal.IntegrityProblem}");
            text.AppendLine();
        }

        var chartMax = (int)Math.Ceiling(Math.Max(requiredCorrectAnswers, days.Max(day => day.Correct)) / 5.0) * 5;

        text.AppendLine("```mermaid");
        text.AppendLine("xychart-beta");
        text.AppendLine("    title \"Richtige Antworten pro Tag (Linie = Tagesziel)\"");
        text.AppendLine($"    x-axis [{string.Join(", ", days.Select(day => $"\"{day.Date.ToString("dd.MM", German)}\""))}]");
        text.AppendLine($"    y-axis \"Antworten\" 0 --> {chartMax}");
        text.AppendLine($"    bar [{string.Join(", ", days.Select(day => day.Correct))}]");
        text.AppendLine($"    line [{string.Join(", ", days.Select(_ => requiredCorrectAnswers))}]");
        text.AppendLine("```");
        text.AppendLine();

        text.AppendLine("| Tag | Richtig | Falsch | Tagesziel |");
        text.AppendLine("|---|---:|---:|:---:|");

        foreach (var day in Enumerable.Reverse(days))
        {
            var goal = day.GoalReached ? "✅" : day.Learned ? "❌" : "– nicht gelernt";
            text.AppendLine($"| {day.Date.ToString("ddd dd.MM.", German)} | {day.Correct} | {day.Wrong} | {goal} |");
        }

        return text.ToString();
    }
}
