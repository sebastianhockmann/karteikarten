namespace EnglishSteamTrainer.Core;

public sealed record QuizContent(
    IReadOnlyList<VocabularyCard> Vocabulary,
    IReadOnlyList<TenseQuestion> Grammar,
    IReadOnlyList<VerbForms> Verbs);

/// <param name="Account">Windows-Kontoname, für Lernplan-Einträge pro Kind.</param>
/// <param name="OnlyKind">Testmodus "--only": nur diese Art von Aufgaben (<see cref="JournalKind"/>).</param>
public sealed record QuizRequest(
    LearningLanguage Language,
    string Account,
    DateOnly Today,
    QuizContent Content,
    TrainerConfig Config,
    LearningPlan Plan,
    IReadOnlyList<JournalEntry> Journal,
    string? OnlyKind = null);

public sealed class QuizSession
{
    internal QuizSession(List<QuizItem> items, PlanEntry? focus, int heldBack, int focusItems)
    {
        Items = items;
        Focus = focus;
        HeldBackCount = heldBack;
        FocusItemCount = focusItems;
    }

    public List<QuizItem> Items { get; }

    /// <summary>Aktiver Lernplan-Eintrag, nur gesetzt, wenn es dazu auch Aufgaben gibt.</summary>
    public PlanEntry? Focus { get; }

    /// <summary>Aufgaben, die laut Lernplan erst später drankommen.</summary>
    public int HeldBackCount { get; }

    public int FocusItemCount { get; }

    public bool IsFocus(QuizItem item) => FocusTopicOf(item) is not null;

    /// <summary>Das erste Thema der Aufgabe, das gerade im Lernplan dran ist.</summary>
    public string? FocusTopicOf(QuizItem item) =>
        Focus is null ? null : QuizPlanner.TopicsOf(item).FirstOrDefault(Focus.HasTopic);
}

/// <summary>
/// Stellt die Aufgaben einer Runde zusammen:
/// 1. Zuletzt falsch beantwortete Aufgaben (aller Arten).
/// 2. Fällige Wiederholungen und neue Aufgaben laut Karteikasten (<see cref="ReviewSchedule"/>);
///    davon kommen <see cref="TrainerConfig.FocusSharePercent"/> % aus den Themen, die gerade im
///    Lernplan dran sind. Grammatik und Verben werden dazwischen verteilt.
/// 3. Danach als Lückenfüller die übrigen Vokabeln, die am frühesten fälligen zuerst.
/// Was laut Lernplan erst später drankommt, fehlt ganz (<see cref="LearnerPlan.IsHeldBack"/>).
/// </summary>
public static class QuizPlanner
{
    // Grammatik und Verben werden mindestens über so viele Plätze verteilt.
    private const int MinimumSpread = 10;

    public static QuizSession Build(QuizRequest request, Random random)
    {
        var today = request.Today;
        var language = request.Language;
        var config = request.Config;
        var plan = request.Plan.WithLegacyFocusTopics(config.FocusTopics).For(language, request.Account);

        bool Included(string kind) => request.OnlyKind is null || request.OnlyKind == kind;

        var vocabulary = Included(JournalKind.Vocabulary) ? request.Content.Vocabulary : [];
        var grammar = Included(JournalKind.Grammar) ? request.Content.Grammar : [];
        var verbs = Included(JournalKind.VerbForms) ? request.Content.Verbs : [];

        var heldBack = vocabulary.Count(card => plan.IsHeldBack(card.Topics, today))
                       + grammar.Count(question => plan.IsHeldBack([question.Hint], today))
                       + verbs.Count(verb => plan.IsHeldBack(verb.Topics, today));

        vocabulary = vocabulary.Where(card => !plan.IsHeldBack(card.Topics, today)).ToList();
        grammar = grammar.Where(question => !plan.IsHeldBack([question.Hint], today)).ToList();
        verbs = verbs.Where(verb => !plan.IsHeldBack(verb.Topics, today)).ToList();

        var entry = plan.ActiveEntry(today);
        var focusItems = entry is null
            ? 0
            : vocabulary.Count(card => card.Topics?.Any(entry.HasTopic) == true)
              + grammar.Count(question => entry.HasTopic(question.Hint))
              + verbs.Count(verb => verb.Topics?.Any(entry.HasTopic) == true);

        // Ohne passende Aufgaben wäre die Anzeige "Thema der Woche" irreführend.
        var focus = focusItems > 0 ? entry : null;

        bool IsFocus(IEnumerable<string>? topics) => focus is not null && topics?.Any(focus.HasTopic) == true;

        var share = config.FocusSharePercent;
        var journal = request.Journal;

        var vocabOrder = Order(
            vocabulary, card => card.Key, card => IsFocus(card.Topics),
            ReviewSchedule.Build(journal, language.Code, JournalKind.Vocabulary), today, share, random);

        var grammarOrder = Order(
            grammar, question => question.Key, question => IsFocus([question.Hint]),
            ReviewSchedule.Build(journal, language.Code, JournalKind.Grammar), today, share, random);

        var verbOrder = Order(
            verbs, verb => verb.Key, verb => IsFocus(verb.Topics),
            ReviewSchedule.Build(journal, language.Code, JournalKind.VerbForms), today, share, random);

        var grammarPicked = grammarOrder.All.Take(config.GrammarQuestionsPerSession).ToList();
        var verbsPicked = verbOrder.All.Take(config.VerbFormQuestionsPerSession).ToList();

        var repeats = vocabOrder.Repeat.Select(card => Vocab(card, random))
            .Concat(grammarPicked.Where(grammarOrder.Repeat.Contains).Select(question => Tense(question, random)))
            .Concat(verbsPicked.Where(verbOrder.Repeat.Contains).Select(verb => Verb(verb, random)))
            .OrderBy(_ => random.Next())
            .ToList();

        var extras = grammarPicked.Where(question => !grammarOrder.Repeat.Contains(question)).Select(question => Tense(question, random))
            .Concat(verbsPicked.Where(verb => !verbOrder.Repeat.Contains(verb)).Select(verb => Verb(verb, random)))
            .OrderBy(_ => random.Next())
            .ToList();

        var rest = QuizOrder.Interleave(
            vocabOrder.Active.Concat(vocabOrder.Later).Select(card => Vocab(card, random)).ToList(),
            extras,
            Math.Max(vocabOrder.Active.Count, MinimumSpread),
            random);

        return new QuizSession([.. repeats, .. rest], focus, heldBack, focusItems);
    }

    public static IReadOnlyList<string> TopicsOf(QuizItem item) => item switch
    {
        VocabQuizItem vocab => vocab.Card.Topics ?? [],
        TenseQuizItem tense => [tense.Question.Hint],
        VerbFormsQuizItem verb => verb.Verb.Topics ?? [],
        _ => []
    };

    private static QuizItem Vocab(VocabularyCard card, Random random) => new VocabQuizItem(card, random.Next(2) == 0);

    private static QuizItem Tense(TenseQuestion question, Random random) =>
        new TenseQuizItem(question, question.Distractors.Prepend(question.CorrectForm).OrderBy(_ => random.Next()).ToList());

    private static QuizItem Verb(VerbForms verb, Random random) => new VerbFormsQuizItem(verb, random.Next(VerbForms.FormNames.Length));

    private sealed record Ordered<T>(List<T> Repeat, List<T> Active, List<T> Later)
    {
        public IEnumerable<T> All => Repeat.Concat(Active).Concat(Later);
    }

    private static Ordered<T> Order<T>(
        IEnumerable<T> items,
        Func<T, string> key,
        Func<T, bool> isFocus,
        Dictionary<string, ReviewState> states,
        DateOnly today,
        int focusSharePercent,
        Random random)
    {
        var all = items
            .Select(item =>
            {
                var state = states.GetValueOrDefault(key(item));
                return (Item: item, State: state, Status: ReviewSchedule.StatusOf(state, today));
            })
            .OrderBy(_ => random.Next())
            .ToList();

        // Innerhalb von Thema/Rest: fällige Wiederholungen vor neuen Aufgaben (stabil sortiert,
        // die zufällige Reihenfolge bleibt sonst erhalten).
        List<T> Active(bool focus) => all
            .Where(x => x.Status is ReviewStatus.Due or ReviewStatus.New && isFocus(x.Item) == focus)
            .OrderBy(x => x.Status == ReviewStatus.Due ? 0 : 1)
            .Select(x => x.Item)
            .ToList();

        return new Ordered<T>(
            all.Where(x => x.Status == ReviewStatus.Repeat).Select(x => x.Item).ToList(),
            QuizOrder.MixFocus(Active(focus: true), Active(focus: false), focusSharePercent, random),
            all.Where(x => x.Status == ReviewStatus.Later).OrderBy(x => x.State!.DueOn).Select(x => x.Item).ToList());
    }
}
