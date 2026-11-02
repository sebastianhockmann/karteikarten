using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public class LearningPlanTests
{
    private const string Plan = """
        {
          "schedule": [
            { "from": "2026-11-02", "to": "2026-11-08", "topics": [ "Tiere" ] },
            { "from": "2026-11-02", "to": "2026-11-08", "topics": [ "Party-Essen" ], "language": "es" },
            { "from": "2026-11-02", "to": "2026-11-08", "topics": [ "Familie" ], "accounts": [ "leand" ], "title": "Unit 2" },
            { "from": "2026-11-09", "to": "2026-11-15", "topics": [ "Zahlen", "Presente" ], "language": "en" }
          ]
        }
        """;

    private static LearningPlan Parse(string json = Plan) => LearningPlan.TryParse(json)!;

    [Theory]
    [InlineData("es", "leand", "2026-11-02", "Unit 2")]
    [InlineData("es", "lena", "2026-11-08", "Party-Essen")]
    [InlineData("en", "tiago", "2026-11-05", "Tiere")]
    [InlineData("en", "tiago", "2026-11-09", "Zahlen, Presente")]
    [InlineData("es", "lena", "2026-11-09", null)]
    [InlineData("es", "lena", "2026-11-01", null)]
    public void Entry_for_a_child_wins_over_language_wins_over_all(string language, string account, string date, string? expected)
    {
        var entry = Parse().For(Languages.Find(language)!, account).ActiveEntry(DateOnly.Parse(date));

        Assert.Equal(expected, entry?.DisplayName);
    }

    [Theory]
    [InlineData("""{ "from": "2026-11-08", "to": "2026-11-02", "topics": [ "Tiere" ] }""")]
    [InlineData("""{ "from": "2026-11-02", "to": "2026-11-08", "topics": [ "" ] }""")]
    [InlineData("""{ "from": "2026-11-02", "to": "2026-11-08", "topics": [] }""")]
    [InlineData("""{ "to": "2026-11-08", "topics": [ "Tiere" ] }""")]
    [InlineData("""{ "from": "2026-11-02", "to": "2026-11-08", "topics": [ "Tiere" ], "language": "spanish" }""")]
    [InlineData("""{ "from": "2026-11-02", "to": "2026-11-08", "topics": [ "Tiere" ], "accounts": [ " " ] }""")]
    [InlineData("""{ "from": "02.11.2026", "to": "08.11.2026", "topics": [ "Tiere" ] }""")]
    public void Typo_in_plan_rejects_the_file(string entry)
    {
        Assert.Null(LearningPlan.TryParse("""{ "schedule": [ ENTRY ] }""".Replace("ENTRY", entry)));
    }

    [Fact]
    public void Empty_plan_is_valid()
    {
        Assert.NotNull(LearningPlan.TryParse("{}"));
        Assert.NotNull(LearningPlan.TryParse("""{ "schedule": [] }"""));
    }

    [Fact]
    public void Planned_topics_are_held_back_until_they_start()
    {
        var plan = Parse().For(Languages.English, "tiago");
        var before = DateOnly.Parse("2026-11-08");
        var during = DateOnly.Parse("2026-11-09");

        Assert.True(plan.IsHeldBack(["Zahlen"], before));
        Assert.False(plan.IsHeldBack(["Zahlen"], during));
        Assert.False(plan.IsHeldBack(["Zahlen"], DateOnly.Parse("2026-12-24")));

        // Nicht geplant oder ohne Thema: immer dabei.
        Assert.False(plan.IsHeldBack(["Farben"], before));
        Assert.False(plan.IsHeldBack([], before));
        Assert.False(plan.IsHeldBack(null, before));

        // Ein Thema schon begonnen oder gar nicht geplant reicht.
        Assert.False(plan.IsHeldBack(["Zahlen", "Tiere"], before));
        Assert.False(plan.IsHeldBack(["Zahlen", "Farben"], before));
    }

    [Fact]
    public void Holding_back_can_be_switched_off()
    {
        var plan = LearningPlan.TryParse(Plan.Replace("\"schedule\"", "\"holdBackPlannedTopics\": false, \"schedule\""))!;

        Assert.False(plan.For(Languages.English, "tiago").IsHeldBack(["Zahlen"], DateOnly.Parse("2026-11-01")));
    }

    [Fact]
    public void Plan_of_another_child_does_not_hold_back()
    {
        var plan = Parse().For(Languages.Spanish, "lena");

        Assert.False(plan.IsHeldBack(["Familie"], DateOnly.Parse("2026-10-01")));
        Assert.True(Parse().For(Languages.Spanish, "LEAND").IsHeldBack(["familie"], DateOnly.Parse("2026-10-01")));
    }

    [Fact]
    public void Legacy_focus_topics_from_config_still_count()
    {
        var config = TrainerConfig.TryParse("""
            {
              "blockedApps": [ { "name": "Steam", "executables": [ "steam.exe" ] } ],
              "focusTopics": [ { "from": "2026-12-01", "to": "2026-12-07", "topic": "Farben", "language": "en" } ]
            }
            """)!;

        var plan = Parse().WithLegacyFocusTopics(config.FocusTopics).For(Languages.English, "tiago");

        Assert.Equal("Farben", plan.ActiveEntry(DateOnly.Parse("2026-12-03"))?.DisplayName);
        Assert.Null(new LearningPlan().WithLegacyFocusTopics(config.FocusTopics).For(Languages.Spanish, "x").ActiveEntry(DateOnly.Parse("2026-12-03")));
    }

    [Fact]
    public void Writing_and_reading_gives_the_same_plan()
    {
        var plan = Parse();
        var again = LearningPlan.TryParse(plan.ToJson())!;

        Assert.Equal(plan.Schedule.Select(Describe), again.Schedule.Select(Describe));
        Assert.Contains("\"Party-Essen\"", plan.ToJson());
        Assert.DoesNotContain("null", plan.ToJson());
    }

    private static string Describe(PlanEntry entry) =>
        $"{entry.From}|{entry.To}|{entry.Title}|{entry.Language}|{string.Join(",", entry.Accounts ?? [])}|{string.Join(",", entry.Topics)}";
}
