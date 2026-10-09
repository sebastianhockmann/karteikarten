using System.Security.Principal;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public sealed class AnswerJournalTests : IDisposable
{
    private static readonly SecurityIdentifier User = new("S-1-5-21-1000-2000-3000-1001");
    private static readonly SecurityIdentifier OtherUser = new("S-1-5-21-1000-2000-3000-1002");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "est-tests-" + Guid.NewGuid());
    private string File1 => Path.Combine(_folder, "journal.txt");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private AnswerJournal Answer(params AnswerResult[] results)
    {
        var journal = AnswerJournal.Load(User, File1);

        for (var i = 0; i < results.Length; i++)
            journal.RecordAnswer(Languages.English, JournalKind.Vocabulary, $"word{i}", $"word{i} (en→de)", "x", results[i]);

        return journal;
    }

    [Fact]
    public void Xp_follows_the_rules_including_streak_bonus()
    {
        var journal = Answer(Enumerable.Repeat(AnswerResult.Correct, 5).ToArray());

        Assert.Equal([10, 10, 10, 10, 15], journal.Entries.Select(e => e.Xp));
        Assert.Equal(55, journal.TotalXp);
        Assert.Equal(5, journal.CurrentStreak);
    }

    [Fact]
    public void Wrong_answer_resets_streak_and_gives_no_xp()
    {
        var journal = Answer(AnswerResult.Correct, AnswerResult.Correct, AnswerResult.Wrong, AnswerResult.Typo);

        Assert.Equal([10, 10, 0, 5], journal.Entries.Select(e => e.Xp));
        Assert.Equal(1, journal.CurrentStreak);
        Assert.Equal(2, journal.BestStreak);
    }

    [Fact]
    public void Reloaded_journal_has_identical_entries()
    {
        var written = Answer(AnswerResult.Correct, AnswerResult.Wrong, AnswerResult.Typo);
        var loaded = AnswerJournal.Load(User, File1);

        Assert.Null(loaded.IntegrityProblem);
        Assert.Equal(written.TotalXp, loaded.TotalXp);
        Assert.Equal(3, loaded.Entries.Count);
        Assert.Equal(2, loaded.CorrectOn(DateOnly.FromDateTime(DateTime.Now)));
    }

    [Fact]
    public void Edited_xp_is_detected_and_ignored_from_that_line_on()
    {
        Answer(AnswerResult.Correct, AnswerResult.Correct, AnswerResult.Correct);

        var lines = File.ReadAllLines(File1);
        lines[2] = lines[2].Replace("|richtig|10|", "|richtig|9999|");
        File.WriteAllLines(File1, lines);

        var loaded = AnswerJournal.Load(User, File1);

        Assert.NotNull(loaded.IntegrityProblem);
        Assert.Single(loaded.Entries);
        Assert.Equal(10, loaded.TotalXp);
    }

    [Fact]
    public void Deleted_line_is_detected()
    {
        Answer(AnswerResult.Wrong, AnswerResult.Correct, AnswerResult.Correct);

        var lines = File.ReadAllLines(File1).ToList();
        lines.RemoveAt(1);
        File.WriteAllLines(File1, lines);

        Assert.NotNull(AnswerJournal.Load(User, File1).IntegrityProblem);
    }

    [Fact]
    public void Journal_copied_from_another_account_is_rejected()
    {
        Answer(AnswerResult.Correct);

        var loaded = AnswerJournal.Load(OtherUser, File1);

        Assert.NotNull(loaded.IntegrityProblem);
        Assert.Empty(loaded.Entries);
    }

    [Fact]
    public void After_tampering_new_answers_count_again_and_evidence_is_kept()
    {
        Answer(AnswerResult.Correct, AnswerResult.Correct);
        File.AppendAllText(File1, "3|2026-01-01T00:00:00|en|vocab|x|x|x|richtig|500|FAKE" + Environment.NewLine);

        var tampered = AnswerJournal.Load(User, File1);
        tampered.RecordAnswer(Languages.English, JournalKind.Grammar, "I / play", "I / play", "am playing", AnswerResult.Correct);

        var reloaded = AnswerJournal.Load(User, File1);

        Assert.Null(reloaded.IntegrityProblem);
        Assert.Equal(3, reloaded.Entries.Count);
        Assert.Equal(30, reloaded.TotalXp);
        Assert.Single(Directory.GetFiles(_folder, "*manipuliert*"));
    }

    [Fact]
    public void Separators_in_answers_cannot_break_the_format()
    {
        var journal = AnswerJournal.Load(User, File1);
        journal.RecordAnswer(Languages.Spanish, JournalKind.Vocabulary, "perro", "Hund", "a|b\nc", AnswerResult.Wrong);

        var loaded = AnswerJournal.Load(User, File1);

        Assert.Null(loaded.IntegrityProblem);
        Assert.Equal("a/b c", loaded.Entries[0].Given);
        Assert.Equal("es", loaded.Entries[0].Language);
    }

    [Fact]
    public void Import_entry_counts_xp_but_not_for_streak_or_today()
    {
        var journal = AnswerJournal.Load(User, File1);
        journal.RecordImport("alt", 120);
        journal.RecordAnswer(Languages.English, JournalKind.Vocabulary, "dog", "dog", "Hund", AnswerResult.Correct);

        var loaded = AnswerJournal.Load(User, File1);

        Assert.Equal(130, loaded.TotalXp);
        Assert.Equal(1, loaded.CurrentStreak);
        Assert.Equal(1, loaded.CorrectOn(DateOnly.FromDateTime(DateTime.Now)));
        Assert.Null(loaded.Entries[0].Result);
    }

    [Fact]
    public void Items_whose_last_attempt_was_wrong_are_repeated()
    {
        var journal = AnswerJournal.Load(User, File1);
        journal.RecordAnswer(Languages.English, JournalKind.Vocabulary, "dog", "dog", "x", AnswerResult.Wrong);
        journal.RecordAnswer(Languages.English, JournalKind.Vocabulary, "cat", "cat", "x", AnswerResult.Wrong);
        journal.RecordAnswer(Languages.English, JournalKind.Vocabulary, "cat", "cat", "Katze", AnswerResult.Correct);
        journal.RecordAnswer(Languages.Spanish, JournalKind.Vocabulary, "perro", "perro", "x", AnswerResult.Wrong);

        Assert.Equal(["dog"], journal.GetItemsToRepeat(Languages.English, JournalKind.Vocabulary));
    }
}
