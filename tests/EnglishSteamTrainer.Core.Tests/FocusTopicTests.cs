using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public class FocusTopicTests
{
    private const string FocusConfig = """
        {
          "blockedApps": [ { "name": "Steam", "executables": [ "steam.exe" ] } ],
          "focusTopics": [
            { "from": "2026-11-02", "to": "2026-11-08", "topic": "Tiere" },
            { "from": "2026-11-02", "to": "2026-11-08", "topic": "Party-Essen", "language": "es" },
            { "from": "2026-11-09", "to": "2026-11-15", "topic": "Zahlen", "language": "en" }
          ]
        }
        """;

    [Theory]
    [InlineData("es", "2026-11-02", "Party-Essen")]
    [InlineData("es", "2026-11-08", "Party-Essen")]
    [InlineData("en", "2026-11-05", "Tiere")]
    [InlineData("en", "2026-11-09", "Zahlen")]
    [InlineData("es", "2026-11-09", null)]
    [InlineData("es", "2026-11-01", null)]
    public void Language_specific_topic_wins_over_topic_for_all_languages(string language, string date, string? expected)
    {
        var config = TrainerConfig.TryParse(FocusConfig)!;

        Assert.Equal(expected, config.FindFocusTopic(Languages.Find(language)!, DateOnly.Parse(date))?.Topic);
        Assert.Equal(70, config.FocusSharePercent);
    }

    [Theory]
    [InlineData("""{ "from": "2026-11-08", "to": "2026-11-02", "topic": "Tiere" }""")]
    [InlineData("""{ "from": "2026-11-02", "to": "2026-11-08", "topic": "" }""")]
    [InlineData("""{ "to": "2026-11-08", "topic": "Tiere" }""")]
    [InlineData("""{ "from": "2026-11-02", "to": "2026-11-08", "topic": "Tiere", "language": "spanish" }""")]
    [InlineData("""{ "from": "02.11.2026", "to": "08.11.2026", "topic": "Tiere" }""")]
    public void Typo_in_focus_topic_rejects_config(string focus)
    {
        var json = """{ "blockedApps": [ { "name": "Steam", "executables": [ "steam.exe" ] } ], "focusTopics": [ FOCUS ] }"""
            .Replace("FOCUS", focus);

        Assert.Null(TrainerConfig.TryParse(json));
    }

    [Fact]
    public void Topics_column_is_optional_and_may_list_several_topics()
    {
        var cards = VocabularyRepository.Parse("""
            Word;German;Hint;GermanAlternatives;WordAlternatives;Topics
            queso;Käse;Wird aus Milch gemacht;;;Essen|Party-Essen
            perro;Hund;Bellt;
            """);

        Assert.True(cards[0].HasTopic("party-essen"));
        Assert.True(cards[0].HasTopic("Essen"));
        Assert.False(cards[1].HasTopic("Essen"));
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
