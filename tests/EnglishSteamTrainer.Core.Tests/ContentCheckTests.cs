using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public class ContentCheckTests
{
    private static ContentSet Set()
    {
        var set = new ContentSet
        {
            Config = TrainerConfig.TryParse("""
                {
                  "blockedApps": [ { "name": "Steam", "executables": [ "steam.exe" ] } ],
                  "users": [ { "account": "tiago", "language": "en" }, { "account": "leand", "language": "es" } ]
                }
                """)!
        };

        foreach (var language in Languages.All)
        {
            set[language].Vocabulary = [new VocabularyCard("dog", "Hund", "", [], [], ["Tiere"])];
            set[language].Grammar = [new TenseQuestion("he", "play", "plays", ["play", "playing", "played"], "Present")];
            set[language].Verbs = language.HasVerbForms ? [new VerbForms(["go"], ["went"], ["gone"], "gehen")] : [];
        }

        return set;
    }

    private static PlanEntry Entry(string from, string to, string topic, string? language = null, params string[] accounts) =>
        new() { From = DateOnly.Parse(from), To = DateOnly.Parse(to), Topics = [topic], Language = language, Accounts = accounts.Length > 0 ? [.. accounts] : null };

    private static List<string> Errors(ContentSet set) =>
        ContentCheck.Check(set).Where(p => p.Severity == ProblemSeverity.Error).Select(p => p.ToString()).ToList();

    private static List<string> Warnings(ContentSet set) =>
        ContentCheck.Check(set).Where(p => p.Severity == ProblemSeverity.Warning).Select(p => p.ToString()).ToList();

    [Fact]
    public void Valid_set_has_no_problems()
    {
        Assert.Empty(ContentCheck.Check(Set()));
    }

    [Theory]
    [InlineData("Hund;Katze")]
    [InlineData("Hund|Katze")]
    [InlineData("Hund\nKatze")]
    [InlineData("")]
    public void Separators_and_empty_required_fields_are_errors(string german)
    {
        var set = Set();
        set[Languages.English].Vocabulary[0] = set[Languages.English].Vocabulary[0] with { German = german };

        Assert.Single(Errors(set));
    }

    [Fact]
    public void Duplicate_words_and_ambiguous_ids_are_errors()
    {
        var set = Set();
        set[Languages.English].Vocabulary.Add(new VocabularyCard("DOG ", "Hund", "", [], [], []));
        set[Languages.English].Vocabulary.Add(new VocabularyCard("puppy", "Welpe", "", [], [], [], "dog"));

        var errors = Errors(set);

        Assert.Contains(errors, e => e.Contains("3-mal"));
        Assert.Contains(errors, e => e.Contains("Id \"dog\""));
    }

    [Fact]
    public void Grammar_needs_three_different_wrong_answers()
    {
        var set = Set();
        set[Languages.Spanish].Grammar[0] = set[Languages.Spanish].Grammar[0] with { Distractors = ["play", "plays", "played"] };

        Assert.Single(Errors(set));
    }

    [Fact]
    public void Planned_topic_without_items_is_an_error_only_for_the_languages_it_applies_to()
    {
        var set = Set();
        set[Languages.Spanish].Vocabulary[0] = set[Languages.Spanish].Vocabulary[0] with { Topics = ["Animales"] };

        set.Plan.Schedule = [Entry("2026-11-02", "2026-11-08", "Tiere", accounts: "tiago")];
        Assert.Empty(Errors(set));

        set.Plan.Schedule = [Entry("2026-11-02", "2026-11-08", "Tiere")];
        Assert.Contains(Errors(set), e => e.Contains("Spanisch"));

        // Grammatik-Themen zählen auch.
        set.Plan.Schedule = [Entry("2026-11-02", "2026-11-08", "present", "en")];
        Assert.Empty(Errors(set));
    }

    [Fact]
    public void Unknown_account_and_overlaps_are_warnings()
    {
        var set = Set();
        set.Plan.Schedule =
        [
            Entry("2026-11-02", "2026-11-08", "Tiere", "en", "tiagoo"),
            Entry("2026-11-02", "2026-11-08", "Tiere", "en"),
            Entry("2026-11-05", "2026-11-12", "Present", "EN")
        ];

        var warnings = Warnings(set);

        Assert.Contains(warnings, w => w.Contains("tiagoo"));
        Assert.Single(warnings, w => w.Contains("überschneiden"));
        Assert.Empty(Errors(set));
    }

    [Fact]
    public void Unreadable_lines_are_reported_with_line_number()
    {
        var problems = new List<ContentProblem>();

        ContentSet.Parse(path => path switch
        {
            "en/vocabulary.csv" => "Word;German\ndog;Hund\ncat\n# Kommentar\n",
            _ => null
        }, problems);

        Assert.Contains(problems, p => p.Severity == ProblemSeverity.Error && p.Message.StartsWith("Zeile 3 "));
        Assert.Contains(problems, p => p.Severity == ProblemSeverity.Warning && p.Message.StartsWith("Zeile 4 "));
    }

    [Fact]
    public void Csv_columns_round_trip_including_id_and_verb_topics()
    {
        var cards = VocabularyRepository.Parse(VocabularyRepository.Format(
        [
            new VocabularyCard("bunny", "Hase", "Hüpft", ["Kaninchen"], ["rabbit"], ["Tiere", "Ostern"], "rabbit"),
            new VocabularyCard("cat", "Katze", "", [], [], null)
        ]));

        Assert.Equal("rabbit", cards[0].Key);
        Assert.Equal(["Tiere", "Ostern"], cards[0].Topics!);
        Assert.Equal("cat", cards[1].Key);

        var verbs = VerbFormsRepository.Parse(VerbFormsRepository.Format([new VerbForms(["get"], ["got"], ["got", "gotten"], "bekommen", ["Unit 3"])]));
        Assert.Equal(["got", "gotten"], verbs[0].PastParticiple);
        Assert.True(verbs[0].HasTopic("unit 3"));

        var grammar = TenseQuestionRepository.Format([new TenseQuestion("I", "play", "am playing", ["is playing", "are playing", "playing"], TenseQuestionRepository.DefaultTopic)]);
        Assert.Equal("Subject;Verb;Correct;Distractors;Topic\nI;play;am playing;is playing|are playing|playing\n", grammar);
    }

    [Fact]
    public void Old_files_without_new_columns_still_parse()
    {
        var verbs = VerbFormsRepository.Parse("Infinitive;SimplePast;PastParticiple;German\nbe;was|were;been;sein");

        Assert.Null(verbs[0].Topics is { Count: > 0 } ? verbs[0].Topics : null);
        Assert.Null(VocabularyRepository.Parse("Word;German\ndog;Hund")[0].Id);
    }
}
