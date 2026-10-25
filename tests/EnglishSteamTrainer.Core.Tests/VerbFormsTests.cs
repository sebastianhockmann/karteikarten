using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public class VerbFormsTests
{
    private static readonly List<VerbForms> Verbs = VerbFormsRepository.Parse("""
        Infinitive;SimplePast;PastParticiple;German
        be;was|were;been;sein
        bring;brought;brought;bringen
        # Kommentar
        get;got;got|gotten;bekommen
        swim;swam;;schwimmen
        blow;blew;blown
        """);

    [Fact]
    public void Incomplete_lines_are_skipped()
    {
        Assert.Equal(["be", "bring", "get"], Verbs.Select(verb => verb.Key));
        Assert.Equal(["was", "were"], Verbs[0].SimplePast);
        Assert.Equal("was / were", Verbs[0].Display(1));
    }

    [Theory]
    [InlineData("was", AnswerResult.Correct)]
    [InlineData("were", AnswerResult.Correct)]
    [InlineData("was/were", AnswerResult.Correct)]
    [InlineData("Was / Were", AnswerResult.Correct)]
    [InlineData("been", AnswerResult.Wrong)]
    public void Every_alternative_counts(string given, AnswerResult expected)
    {
        var check = AnswerChecker.Check(given, AnswerChecker.ValidAnswers(Verbs[0], 1), Languages.English);

        Assert.Equal(expected, check.Result);
    }

    [Fact]
    public void Infinitive_with_to_is_correct()
    {
        var check = AnswerChecker.Check("to bring", AnswerChecker.ValidAnswers(Verbs[1], 0), Languages.English);

        Assert.Equal(AnswerResult.Correct, check.Result);
    }

    [Fact]
    public void One_wrong_form_makes_the_whole_answer_wrong()
    {
        var correct = new AnswerCheck(AnswerResult.Correct, "brought");
        var typo = new AnswerCheck(AnswerResult.Typo, "brought");
        var wrong = new AnswerCheck(AnswerResult.Wrong, "brought");

        Assert.Equal(AnswerResult.Correct, AnswerChecker.Combine([correct, correct]));
        Assert.Equal(AnswerResult.Typo, AnswerChecker.Combine([correct, typo]));
        Assert.Equal(AnswerResult.Wrong, AnswerChecker.Combine([typo, wrong]));
        Assert.Equal(AnswerResult.Wrong, AnswerChecker.Combine([]));
    }

    [Fact]
    public void Only_languages_with_verb_forms_sync_a_verbs_file()
    {
        Assert.Contains("en/verbs.csv", ContentStore.AllFiles);
        Assert.DoesNotContain("es/verbs.csv", ContentStore.AllFiles);
        Assert.Empty(ContentStore.LoadVerbForms(Languages.Spanish).Value);
    }

    [Fact]
    public void Empty_verbs_file_is_rejected()
    {
        Assert.False(ContentStore.IsValid("en/verbs.csv", "Infinitive;SimplePast;PastParticiple;German\n"));
        Assert.True(ContentStore.IsValid("en/verbs.csv", "Infinitive;SimplePast;PastParticiple;German\nbe;was|were;been;sein\n"));
    }
}
