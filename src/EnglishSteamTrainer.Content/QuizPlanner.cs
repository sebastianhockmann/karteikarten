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
///    Lernplan dran sind - Vokabeln, Grammatik und Verben gleichermaßen. Grammatik und Verben
///    außerhalb des Themas sind auf <see cref="TrainerConfig.GrammarQuestionsPerSession"/> bzw.
///    <see cref="TrainerConfig.VerbFormQuestionsPerSession"/> begrenzt und werden dazwischen verteilt.
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

        var journal = request.Journal;

        var vocabOrder = Order(
            vocabulary, card => card.Key, card => IsFocus(card.Topics),
            ReviewSchedule.Build(journal, language.Code, JournalKind.Vocabulary), today, random);

        var grammarOrder = Order(
            grammar, question => question.Key, question => IsFocus([question.Hint]),
            ReviewSchedule.Build(journal, language.Code, JournalKind.Grammar), today, random);

        var verbOrder = Order(
            verbs, verb => verb.Key, verb => IsFocus(verb.Topics),
            ReviewSchedule.Build(journal, language.Code, JournalKind.VerbForms), today, random);

        // Außerhalb des Themas nur so viele wie eingestellt (Wiederholungen zuerst).
        var grammarPicked = grammarOrder.OutsideFocus.Take(config.GrammarQuestionsPerSession).ToList();
        var verbsPicked = verbOrder.OutsideFocus.Take(config.VerbFormQuestionsPerSession).ToList();

        var repeats = vocabOrder.Repeat.Select(card => Vocab(card, random))
            .Concat(grammarOrder.FocusRepeat.Concat(grammarPicked.Where(grammarOrder.Repeat.Contains)).Select(question => Tense(question, random)))
            .Concat(verbOrder.FocusRepeat.Concat(verbsPicked.Where(verbOrder.Repeat.Contains)).Select(verb => Verb(verb, random)))
            .OrderBy(_ => random.Next())
            .ToList();

        // Thema: alle Arten zusammen, fällige Wiederholungen vor Neuem.
        var focusDue = vocabOrder.FocusDue.Select(card => Vocab(card, random))
            .Concat(grammarOrder.FocusDue.Select(question => Tense(question, random)))
            .Concat(verbOrder.FocusDue.Select(verb => Verb(verb, random)))
            .OrderBy(_ => random.Next());

        var focusNew = vocabOrder.FocusNew.Select(card => Vocab(card, random))
            .Concat(grammarOrder.FocusNew.Select(question => Tense(question, random)))
            .Concat(verbOrder.FocusNew.Select(verb => Verb(verb, random)))
            .OrderBy(_ => random.Next());

        var active = QuizOrder.MixFocus(
            focusDue.Concat(focusNew).ToList(),
            vocabOrder.OtherDue.Concat(vocabOrder.OtherNew).Select(card => Vocab(card, random)).ToList(),
            config.FocusSharePercent,
            random);

        var extras = grammarPicked.Where(question => !grammarOrder.Repeat.Contains(question)).Select(question => Tense(question, random))
            .Concat(verbsPicked.Where(verb => !verbOrder.Repeat.Contains(verb)).Select(verb => Verb(verb, random)))
            .OrderBy(_ => random.Next())
            .ToList();

        var rest = QuizOrder.Interleave(
            [.. active, .. vocabOrder.Later.Select(card => Vocab(card, random))],
            extras,
            Math.Max(active.Count, MinimumSpread),
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

    /// <param name="Repeat">Letzter Versuch falsch (Thema und Rest).</param>
    /// <param name="Later">Noch nicht wieder fällig, die am frühesten fälligen zuerst.</param>
    private sealed record Ordered<T>(
        List<T> Repeat,
        List<T> FocusRepeat,
        List<T> FocusDue,
        List<T> FocusNew,
        List<T> OtherDue,
        List<T> OtherNew,
        List<T> Later,
        List<T> LaterOutsideFocus)
    {
        /// <summary>Kandidaten außerhalb des Themas in Reihenfolge (für die Begrenzung bei Grammatik/Verben).</summary>
        public IEnumerable<T> OutsideFocus => Repeat.Except(FocusRepeat).Concat(OtherDue).Concat(OtherNew).Concat(LaterOutsideFocus);
    }

    private static Ordered<T> Order<T>(
        IEnumerable<T> items,
        Func<T, string> key,
        Func<T, bool> isFocus,
        Dictionary<string, ReviewState> states,
        DateOnly today,
        Random random)
    {
        var all = items
            .Select(item =>
            {
                var state = states.GetValueOrDefault(key(item));
                return (Item: item, State: state, Status: ReviewSchedule.StatusOf(state, today), Focus: isFocus(item));
            })
            .OrderBy(_ => random.Next())
            .ToList();

        List<T> Pick(ReviewStatus status, bool? focus) =>
            all.Where(x => x.Status == status && (focus is null || x.Focus == focus)).Select(x => x.Item).ToList();

        var later = all.Where(x => x.Status == ReviewStatus.Later).OrderBy(x => x.State!.DueOn).ToList();

        return new Ordered<T>(
            Pick(ReviewStatus.Repeat, null),
            Pick(ReviewStatus.Repeat, true),
            Pick(ReviewStatus.Due, true),
            Pick(ReviewStatus.New, true),
            Pick(ReviewStatus.Due, false),
            Pick(ReviewStatus.New, false),
            later.Select(x => x.Item).ToList(),
            later.Where(x => !x.Focus).Select(x => x.Item).ToList());
    }
}
