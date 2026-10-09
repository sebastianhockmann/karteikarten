namespace EnglishSteamTrainer.Core;

/// <summary>
/// Die einzigen Regeln, nach denen es Punkte gibt. Jede Vergabe steht mit Grund im
/// Lernjournal, die Summe ist also jederzeit nachrechenbar.
/// </summary>
public static class XpRules
{
    public const int CorrectAnswer = 10;
    public const int TypoAnswer = 5;
    public const int StreakBonus = 5;
    public const int StreakLength = 5;
    public const int XpPerLevel = 100;

    public static readonly string[] Explanation =
    [
        $"Richtige Antwort: +{CorrectAnswer} XP",
        $"Richtig, aber mit kleinem Tippfehler: +{TypoAnswer} XP",
        $"Jede {StreakLength}. richtige Antwort in Folge: +{StreakBonus} Bonus-XP",
        "Falsche Antwort: 0 XP, die Serie beginnt von vorn",
        "Überspringen oder Tipp anzeigen: keine Punkte, kein Abzug",
        $"Alle {XpPerLevel} XP steigst du ein Level auf"
    ];

    /// <param name="streakAfter">Serie inklusive dieser Antwort (0 bei falscher Antwort).</param>
    public static int ForAnswer(AnswerResult result, int streakAfter)
    {
        var xp = result switch
        {
            AnswerResult.Correct => CorrectAnswer,
            AnswerResult.Typo => TypoAnswer,
            _ => 0
        };

        if (result != AnswerResult.Wrong && streakAfter > 0 && streakAfter % StreakLength == 0)
            xp += StreakBonus;

        return xp;
    }

    public static int Level(int xp) => Math.Max(1, (xp / XpPerLevel) + 1);

    public static string Rank(int level, LearningLanguage language) => level switch
    {
        <= 1 => "Starter",
        2 => "Wort-Entdecker",
        3 => "Vokabel-Held",
        4 => $"{language.Name}-Profi",
        _ => "Steam-Meister"
    };
}
