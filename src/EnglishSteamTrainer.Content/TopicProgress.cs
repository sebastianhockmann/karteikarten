namespace EnglishSteamTrainer.Core;

/// <param name="Items">Aufgaben zum Thema (Vokabeln, Grammatik, Verben).</param>
/// <param name="Seen">Davon mindestens einmal beantwortet.</param>
/// <param name="Mastered">Davon "sicher" (Fach <see cref="ReviewSchedule.MasteredBox"/> oder höher).</param>
public sealed record TopicProgress(PlanEntry Entry, string Topic, int Items, int Seen, int Mastered)
{
    public int MasteredPercent => Items == 0 ? 0 : (int)Math.Round(100.0 * Mastered / Items);

    /// <summary>
    /// Fortschritt je Thema für die Lernplan-Einträge eines Kindes, die zwischen
    /// <paramref name="from"/> und <paramref name="to"/> liegen (nach Beginn sortiert).
    /// </summary>
    public static List<TopicProgress> Compute(
        LearnerPlan plan,
        LearningLanguage language,
        QuizContent content,
        IReadOnlyList<JournalEntry> journal,
        DateOnly from,
        DateOnly to)
    {
        var vocab = ReviewSchedule.Build(journal, language.Code, JournalKind.Vocabulary);
        var grammar = ReviewSchedule.Build(journal, language.Code, JournalKind.Grammar);
        var verbs = ReviewSchedule.Build(journal, language.Code, JournalKind.VerbForms);

        return plan.Entries
            .Where(entry => entry.To >= from && entry.From <= to)
            .OrderBy(entry => entry.From)
            .SelectMany(entry => entry.Topics.Select(topic =>
            {
                var states = content.Vocabulary.Where(card => card.HasTopic(topic)).Select(card => vocab.GetValueOrDefault(card.Key))
                    .Concat(content.Grammar.Where(question => question.HasTopic(topic)).Select(question => grammar.GetValueOrDefault(question.Key)))
                    .Concat(content.Verbs.Where(verb => verb.HasTopic(topic)).Select(verb => verbs.GetValueOrDefault(verb.Key)))
                    .ToList();

                return new TopicProgress(
                    entry,
                    topic,
                    states.Count,
                    states.Count(state => state is not null),
                    states.Count(state => state?.IsMastered == true));
            }))
            .ToList();
    }
}
