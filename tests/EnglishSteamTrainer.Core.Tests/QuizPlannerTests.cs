using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public class QuizPlannerTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    private static VocabularyCard Card(string word, params string[] topics) =>
        new(word, word + "-de", "", [], [], [.. topics]);

    private static TenseQuestion Grammar(string subject, string topic) =>
        new(subject, "play", "plays", ["play", "playing", "played"], topic);

    private static VerbForms Verb(string infinitive, params string[] topics) =>
        new([infinitive], [infinitive + "ed"], [infinitive + "ed"], "x", [.. topics]);

    private static JournalEntry Answer(int daysAgo, string item, AnswerResult result, string kind = JournalKind.Vocabulary) =>
        new(0, Today.AddDays(-daysAgo).ToDateTime(new TimeOnly(15, 0)), "en", kind, item, "", "", result, 0);

    private static QuizSession Build(
        QuizContent content,
        LearningPlan? plan = null,
        IReadOnlyList<JournalEntry>? journal = null,
        TrainerConfig? config = null,
        string? only = null,
        int seed = 1) =>
        QuizPlanner.Build(
            new QuizRequest(Languages.English, "tiago", Today, content, config ?? new TrainerConfig(), plan ?? new LearningPlan(), journal ?? [], only),
            new Random(seed));

    private static LearningPlan Plan(string from, string to, params string[] topics) =>
        new() { Schedule = [new PlanEntry { From = DateOnly.Parse(from), To = DateOnly.Parse(to), Topics = [.. topics] }] };

    private static List<string> Words(QuizSession session) =>
        session.Items.OfType<VocabQuizItem>().Select(item => item.Card.Word).ToList();

    [Fact]
    public void Last_wrong_answers_come_first_then_due_and_new_then_later()
    {
        var content = new QuizContent([Card("later"), Card("new"), Card("due"), Card("wrong")], [], []);
        var journal = new[]
        {
            Answer(0, "later", AnswerResult.Correct), // Fach 1, erst morgen fällig
            Answer(3, "due", AnswerResult.Correct),   // Fach 1, seit 2 Tagen fällig
            Answer(1, "wrong", AnswerResult.Wrong)
        };

        for (var seed = 0; seed < 20; seed++)
        {
            var words = Words(Build(content, journal: journal, seed: seed));

            Assert.Equal("wrong", words[0]);
            Assert.Equal("due", words[1]);
            Assert.Equal("new", words[2]);
            Assert.Equal("later", words[3]);
        }
    }

    [Fact]
    public void Future_topics_are_held_back_and_counted()
    {
        var content = new QuizContent(
            [Card("cat", "Tiere"), Card("one", "Zahlen"), Card("red")],
            [Grammar("he", "Present Progressive")],
            [Verb("go", "Zahlen")]);

        var session = Build(content, Plan("2026-11-09", "2026-11-15", "Zahlen", "Present Progressive"));

        Assert.Equal(["cat", "red"], Words(session).Order());
        Assert.Empty(session.Items.OfType<TenseQuizItem>());
        Assert.Empty(session.Items.OfType<VerbFormsQuizItem>());
        Assert.Equal(3, session.HeldBackCount);
        Assert.Null(session.Focus);
    }

    [Fact]
    public void Active_topic_comes_first_and_is_marked()
    {
        var content = new QuizContent(
            [.. Enumerable.Range(0, 20).Select(i => Card("other" + i)), Card("cat", "Tiere"), Card("dog", "Tiere")],
            [],
            []);

        var session = Build(content, Plan("2026-11-02", "2026-11-08", "Tiere"), config: new TrainerConfig { FocusSharePercent = 100 });

        Assert.Equal(["cat", "dog"], Words(session).Take(2).Order());
        Assert.Equal("Tiere", session.Focus?.DisplayName);
        Assert.Equal(2, session.FocusItemCount);
        Assert.Equal("Tiere", session.FocusTopicOf(session.Items[0]));
        Assert.Null(session.FocusTopicOf(session.Items[^1]));
    }

    [Fact]
    public void Focus_is_only_shown_if_there_are_items_for_it()
    {
        var session = Build(new QuizContent([Card("cat")], [], []), Plan("2026-11-02", "2026-11-08", "Weltraum"));

        Assert.Null(session.Focus);
    }

    [Fact]
    public void Grammar_and_verbs_are_limited_and_spread_among_the_words()
    {
        var content = new QuizContent(
            [.. Enumerable.Range(0, 40).Select(i => Card("w" + i))],
            [.. Enumerable.Range(0, 8).Select(i => Grammar("s" + i, "Present"))],
            [.. Enumerable.Range(0, 8).Select(i => Verb("v" + i))]);

        // Nur w0 bis w9 sind neu, der Rest wurde heute schon geübt (Lückenfüller).
        var journal = Enumerable.Range(10, 30).Select(i => Answer(0, "w" + i, AnswerResult.Correct)).ToList();

        var session = Build(content, journal: journal, config: new TrainerConfig { GrammarQuestionsPerSession = 3, VerbFormQuestionsPerSession = 2 });

        Assert.Equal(3, session.Items.OfType<TenseQuizItem>().Count());
        Assert.Equal(2, session.Items.OfType<VerbFormsQuizItem>().Count());
        Assert.Equal(45, session.Items.Count);

        var lastExtra = session.Items.FindLastIndex(item => item is not VocabQuizItem);
        Assert.True(lastExtra < 15, "Grammatik und Verben verteilen sich über die neuen Vokabeln, nicht über die Lückenfüller.");
        Assert.Equal(Enumerable.Range(0, 10).Select(i => "w" + i).Order(), Words(session).Take(10).Order());
        Assert.All(session.Items.OfType<TenseQuizItem>(), item => Assert.Contains("plays", item.ShuffledOptions));
    }

    [Fact]
    public void Verbs_in_the_active_topic_are_not_limited_and_mixed_in_by_share()
    {
        var content = new QuizContent(
            [.. Enumerable.Range(0, 30).Select(i => Card("w" + i))],
            [.. Enumerable.Range(0, 8).Select(i => Grammar("s" + i, "Present"))],
            [.. Enumerable.Range(0, 20).Select(i => Verb("v" + i, "Unregelmäßige Verben")), Verb("ohne")]);

        var session = Build(
            content,
            Plan("2026-11-02", "2026-11-08", "Unregelmäßige Verben"),
            config: new TrainerConfig { GrammarQuestionsPerSession = 2, VerbFormQuestionsPerSession = 1 });

        var verbs = session.Items.OfType<VerbFormsQuizItem>().ToList();

        // Alle 20 Themenverben, das Verb ohne Thema zählt zur Obergrenze von 1.
        Assert.Equal(20, verbs.Count(v => v.Verb.HasTopic("Unregelmäßige Verben")));
        Assert.True(verbs.Count(v => !v.Verb.HasTopic("Unregelmäßige Verben")) <= 1);
        Assert.Equal(2, session.Items.OfType<TenseQuizItem>().Count());

        // Etwa 70 % der ersten Aufgaben sind Themenverben.
        Assert.True(session.Items.Take(15).Count(item => item is VerbFormsQuizItem) >= 7);
        Assert.Equal("Unregelmäßige Verben", session.Focus?.DisplayName);
    }

    [Fact]
    public void Learned_topic_verbs_wait_until_they_are_due()
    {
        var content = new QuizContent([Card("cat")], [], [Verb("go", "Unregelmäßige Verben"), Verb("be", "Unregelmäßige Verben")]);

        var session = Build(
            content,
            Plan("2026-11-02", "2026-11-08", "Unregelmäßige Verben"),
            journal: [Answer(0, "go", AnswerResult.Correct, JournalKind.VerbForms)]);

        Assert.Equal(["be"], session.Items.OfType<VerbFormsQuizItem>().Select(v => v.Verb.Key));
    }

    [Fact]
    public void Grammar_answered_wrong_is_picked_first()
    {
        var content = new QuizContent(
            [Card("cat")],
            [.. Enumerable.Range(0, 8).Select(i => Grammar("s" + i, "Present"))],
            []);

        var session = Build(
            content,
            journal: [Answer(1, "s5 / play", AnswerResult.Wrong, JournalKind.Grammar)],
            config: new TrainerConfig { GrammarQuestionsPerSession = 1 });

        var grammar = Assert.Single(session.Items.OfType<TenseQuizItem>());
        Assert.Equal("s5", grammar.Question.Subject);
        Assert.Same(grammar, session.Items[0]);
    }

    [Fact]
    public void Renamed_word_keeps_its_history_through_the_id()
    {
        var content = new QuizContent([new VocabularyCard("bunny", "Hase", "", [], [], null, "rabbit"), Card("new")], [], []);

        var words = Words(Build(content, journal: [Answer(1, "rabbit", AnswerResult.Wrong)]));

        Assert.Equal("bunny", words[0]);
    }

    [Fact]
    public void Only_option_limits_the_kind()
    {
        var content = new QuizContent([Card("cat")], [Grammar("he", "Present")], [Verb("go")]);

        var session = Build(content, only: JournalKind.VerbForms);

        Assert.IsType<VerbFormsQuizItem>(Assert.Single(session.Items));
    }

    [Fact]
    public void Interleave_keeps_both_orders()
    {
        var main = Enumerable.Range(0, 10).Select(i => "m" + i).ToList();
        var extra = new List<string> { "e0", "e1", "e2" };

        var result = QuizOrder.Interleave(main, extra, 4, new Random(3));

        Assert.Equal(13, result.Count);
        Assert.Equal(main, result.Where(x => x.StartsWith('m')));
        Assert.Equal(extra, result.Where(x => x.StartsWith('e')));
        Assert.True(result.FindLastIndex(x => x.StartsWith('e')) < 7);
    }

    [Fact]
    public void Focus_items_come_first_by_share()
    {
        var focus = Enumerable.Range(0, 10).Select(i => "f" + i).ToList();
        var others = Enumerable.Range(0, 10).Select(i => "o" + i).ToList();

        var all = QuizOrder.MixFocus(focus, others, 100, new Random(1));
        Assert.Equal(focus.Concat(others), all);

        var mixed = QuizOrder.MixFocus(focus, others, 70, new Random(1));
        Assert.Equal(20, mixed.Distinct().Count());
        Assert.Equal(focus, mixed.Where(item => item.StartsWith('f')));
        Assert.True(mixed.Take(10).Count(item => item.StartsWith('f')) > 5);
    }
}
