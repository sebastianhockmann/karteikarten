namespace EnglishSteamTrainer.Core;

public enum ReviewStatus
{
    /// <summary>Letzter Versuch falsch - kommt zuerst.</summary>
    Repeat,

    /// <summary>Wiederholung ist fällig.</summary>
    Due,

    /// <summary>Noch nie beantwortet.</summary>
    New,

    /// <summary>Kommt erst später wieder (nur als Lückenfüller, wenn alles andere erledigt ist).</summary>
    Later
}

/// <param name="Box">Fach im Karteikasten, 1 bis <see cref="ReviewSchedule.MaxBox"/>.</param>
/// <param name="LastReviewed">Tag der letzten Antwort, die das Fach bestimmt hat.</param>
public sealed record ReviewState(int Box, DateOnly LastReviewed, bool LastWrong)
{
    public DateOnly DueOn => LastWrong ? LastReviewed : LastReviewed.AddDays(ReviewSchedule.IntervalDays(Box));

    public bool IsMastered => !LastWrong && Box >= ReviewSchedule.MasteredBox;
}

/// <summary>
/// Karteikasten nach Leitner, berechnet aus dem Lernjournal: Richtig, wenn die Karte
/// fällig (oder neu) war, rückt sie ein Fach weiter und kommt seltener. Falsch = zurück in
/// Fach 1. Wer eine nicht fällige Karte als Lückenfüller richtig beantwortet, ändert
/// nichts - sonst würde eine Karte an einem Tag gleich mehrere Fächer überspringen.
/// </summary>
public static class ReviewSchedule
{
    public const int MaxBox = 5;

    /// <summary>Ab diesem Fach gilt eine Karte als "sicher" (Themenfortschritt im Bericht).</summary>
    public const int MasteredBox = 3;

    // Tage bis zur nächsten Wiederholung, je Fach.
    private static readonly int[] Intervals = [0, 1, 3, 7, 14, 30];

    public static int IntervalDays(int box) => Intervals[Math.Clamp(box, 1, MaxBox)];

    /// <summary>Zustand je Aufgabe (Schlüssel = Spalte "Aufgabe" im Journal) für eine Sprache und Art.</summary>
    public static Dictionary<string, ReviewState> Build(IEnumerable<JournalEntry> entries, string languageCode, string kind)
    {
        var states = new Dictionary<string, ReviewState>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (entry.Result is not { } result || entry.Language != languageCode || entry.Kind != kind)
                continue;

            var day = DateOnly.FromDateTime(entry.Time);
            states.TryGetValue(entry.Item, out var state);
            states[entry.Item] = Next(state, result, day);
        }

        return states;
    }

    internal static ReviewState Next(ReviewState? state, AnswerResult result, DateOnly day)
    {
        if (result == AnswerResult.Wrong)
            return new ReviewState(1, day, LastWrong: true);

        if (state is null)
            return new ReviewState(1, day, LastWrong: false);

        // Nach einem Fehler erst wieder ab Fach 1 aufbauen.
        if (state.LastWrong)
            return new ReviewState(1, day, LastWrong: false);

        if (state.DueOn > day)
            return state;

        // Tippfehler: gekonnt, aber noch nicht sicher genug für das nächste Fach.
        var box = result == AnswerResult.Typo ? state.Box : Math.Min(MaxBox, state.Box + 1);
        return new ReviewState(box, day, LastWrong: false);
    }

    public static ReviewStatus StatusOf(ReviewState? state, DateOnly today) => state switch
    {
        null => ReviewStatus.New,
        { LastWrong: true } => ReviewStatus.Repeat,
        _ when state.DueOn <= today => ReviewStatus.Due,
        _ => ReviewStatus.Later
    };
}
