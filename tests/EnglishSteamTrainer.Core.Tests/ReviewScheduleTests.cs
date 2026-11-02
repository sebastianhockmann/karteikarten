using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public class ReviewScheduleTests
{
    private static readonly DateOnly Day0 = new(2026, 11, 2);

    private static JournalEntry Answer(int day, string item, AnswerResult result, string kind = JournalKind.Vocabulary, string language = "en") =>
        new(0, Day0.AddDays(day).ToDateTime(new TimeOnly(15, 0)), language, kind, item, "", "", result, 0);

    private static ReviewState? State(params JournalEntry[] entries) =>
        ReviewSchedule.Build(entries, "en", JournalKind.Vocabulary).GetValueOrDefault("dog");

    [Fact]
    public void Correct_answers_on_due_days_move_the_card_up()
    {
        var state = State(
            Answer(0, "dog", AnswerResult.Correct),   // Fach 1, fällig an Tag 1
            Answer(1, "dog", AnswerResult.Correct),   // Fach 2, fällig an Tag 4
            Answer(4, "dog", AnswerResult.Correct),   // Fach 3, fällig an Tag 11
            Answer(11, "dog", AnswerResult.Correct)); // Fach 4, fällig an Tag 25

        Assert.Equal(4, state!.Box);
        Assert.Equal(Day0.AddDays(25), state.DueOn);
        Assert.True(state.IsMastered);
    }

    [Fact]
    public void Answering_a_card_that_is_not_due_changes_nothing()
    {
        var state = State(
            Answer(0, "dog", AnswerResult.Correct),
            Answer(0, "dog", AnswerResult.Correct),
            Answer(0, "dog", AnswerResult.Correct));

        Assert.Equal(1, state!.Box);
        Assert.Equal(ReviewStatus.Later, ReviewSchedule.StatusOf(state, Day0));
        Assert.Equal(ReviewStatus.Due, ReviewSchedule.StatusOf(state, Day0.AddDays(1)));
    }

    [Fact]
    public void Wrong_answer_sends_the_card_back_and_it_must_be_rebuilt()
    {
        var state = State(
            Answer(0, "dog", AnswerResult.Correct),
            Answer(1, "dog", AnswerResult.Correct),
            Answer(4, "dog", AnswerResult.Wrong));

        Assert.Equal(1, state!.Box);
        Assert.Equal(ReviewStatus.Repeat, ReviewSchedule.StatusOf(state, Day0.AddDays(4)));

        // Gleich danach richtig: zurück auf Fach 1, morgen wieder fällig.
        state = State(
            Answer(0, "dog", AnswerResult.Correct),
            Answer(1, "dog", AnswerResult.Correct),
            Answer(4, "dog", AnswerResult.Wrong),
            Answer(4, "dog", AnswerResult.Correct));

        Assert.Equal(1, state!.Box);
        Assert.Equal(ReviewStatus.Due, ReviewSchedule.StatusOf(state, Day0.AddDays(5)));
    }

    [Fact]
    public void Typo_keeps_the_card_in_its_box()
    {
        var state = State(
            Answer(0, "dog", AnswerResult.Correct),
            Answer(1, "dog", AnswerResult.Typo));

        Assert.Equal(1, state!.Box);
        Assert.Equal(Day0.AddDays(2), state.DueOn);
    }

    [Fact]
    public void Box_is_capped()
    {
        var entries = new List<JournalEntry>();
        var day = 0;

        for (var i = 0; i < 10; i++)
        {
            entries.Add(Answer(day, "dog", AnswerResult.Correct));
            day += 31;
        }

        Assert.Equal(ReviewSchedule.MaxBox, State([.. entries])!.Box);
    }

    [Fact]
    public void Only_entries_of_the_language_and_kind_count_and_keys_ignore_case()
    {
        var states = ReviewSchedule.Build(
        [
            Answer(0, "Dog", AnswerResult.Wrong),
            Answer(0, "dog", AnswerResult.Correct, language: "es"),
            Answer(0, "dog", AnswerResult.Correct, kind: JournalKind.Grammar)
        ], "en", JournalKind.Vocabulary);

        Assert.Single(states);
        Assert.True(states["DOG"].LastWrong);
        Assert.Equal(ReviewStatus.New, ReviewSchedule.StatusOf(null, Day0));
    }
}
