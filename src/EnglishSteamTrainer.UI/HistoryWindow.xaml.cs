using System.Linq;
using System.Windows;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.UI;

/// <summary>
/// Zeigt, woher jeder einzelne Punkt kommt - direkt aus dem Lernjournal.
/// </summary>
public partial class HistoryWindow : Window
{
    public HistoryWindow(AnswerJournal journal, TrainerConfig config)
    {
        InitializeComponent();

        var xp = journal.TotalXp;
        var level = XpRules.Level(xp);
        var days = journal.GetDaySummaries();
        var required = config.RequiredCorrectAnswersFor(System.Environment.MachineName, System.Environment.UserName);

        TotalXpText.Text = xp.ToString();
        LevelValueText.Text = level.ToString();
        LevelLabelText.Text = $"Level · noch {XpRules.XpPerLevel - xp % XpRules.XpPerLevel} XP bis Level {level + 1}";
        BestStreakValueText.Text = journal.BestStreak.ToString();
        GoalDaysText.Text = days.Count(day => day.Correct + day.Typos >= required).ToString();

        RulesList.ItemsSource = XpRules.Explanation;

        if (journal.IntegrityProblem is not null)
        {
            IntegrityText.Text = "⚠️ Das Lernjournal wurde außerhalb der App verändert. " + journal.IntegrityProblem;
            IntegrityBox.Visibility = Visibility.Visible;
        }

        DaysGrid.ItemsSource = days.Select(day =>
        {
            var answered = day.Correct + day.Typos + day.Wrong;

            return new
            {
                Date = day.Date.ToString("ddd, dd.MM.yyyy"),
                day.Correct,
                day.Typos,
                day.Wrong,
                Rate = answered == 0 ? "" : $"{100 * (day.Correct + day.Typos) / answered} %",
                day.Xp,
                Goal = answered == 0 ? "" : day.Correct + day.Typos >= required ? "✅ geschafft" : "–"
            };
        }).ToList();

        var runningXp = 0;
        var entries = journal.Entries.Select(entry =>
        {
            runningXp += entry.Xp;

            return new
            {
                entry.Sequence,
                Time = entry.Time.ToString("dd.MM.yyyy HH:mm"),
                Language = Languages.Find(entry.Language)?.Name ?? "",
                Prompt = entry.Kind == JournalKind.Import ? entry.Item : entry.Prompt,
                entry.Given,
                Result = entry.Result switch
                {
                    AnswerResult.Correct => "✅ richtig",
                    AnswerResult.Typo => "✏️ Tippfehler",
                    AnswerResult.Wrong => "❌ falsch",
                    _ => "📥 Übernahme"
                },
                Xp = entry.Xp > 0 ? $"+{entry.Xp}" : "0",
                RunningXp = runningXp
            };
        }).Reverse().ToList();

        EntriesGrid.ItemsSource = entries;

        MistakesGrid.ItemsSource = journal.GetMistakes(50).Select(mistake => new
        {
            Language = Languages.Find(mistake.Language)?.Name ?? mistake.Language,
            mistake.Item,
            mistake.Wrong,
            mistake.Correct
        }).ToList();

        JournalPathText.Text = $"Lernjournal: {journal.FilePath} · {journal.Entries.Count} Einträge, jede Zeile signiert";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
