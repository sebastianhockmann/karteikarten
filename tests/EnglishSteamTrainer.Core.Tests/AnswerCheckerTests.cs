using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public class AnswerCheckerTests
{
    private static AnswerResult Check(string given, LearningLanguage language, params string[] valid) =>
        AnswerChecker.Check(given, valid, language).Result;

    [Theory]
    [InlineData("Hund")]
    [InlineData("  hund ")]
    [InlineData("der Hund")]
    public void Exact_answers_ignore_case_whitespace_and_articles(string given)
    {
        Assert.Equal(AnswerResult.Correct, Check(given, Languages.English, "Hund"));
    }

    [Theory]
    [InlineData("adios", "adiós")]
    [InlineData("pequeno", "pequeño")]
    [InlineData("el perro", "perro")]
    [InlineData("weiss", "weiß")]
    [InlineData("gruen", "grün")]
    public void Accents_and_umlauts_are_accepted(string given, string valid)
    {
        Assert.Equal(AnswerResult.Correct, Check(given, Languages.Spanish, valid));
    }

    [Fact]
    public void Small_typo_in_long_word_counts_as_typo()
    {
        var check = AnswerChecker.Check("Schwestr", ["Schwester"], Languages.English);

        Assert.Equal(AnswerResult.Typo, check.Result);
        Assert.Equal("Schwester", check.Expected);
    }

    [Theory]
    [InlineData("Mund", "Hund")]
    [InlineData("car", "cat")]
    [InlineData("", "Hund")]
    [InlineData("der", "Hund")]
    public void Short_words_need_exact_answer(string given, string valid)
    {
        Assert.Equal(AnswerResult.Wrong, Check(given, Languages.English, valid));
    }

    [Fact]
    public void Alternatives_are_accepted()
    {
        var card = new VocabularyCard("happy", "glücklich", "", ["fröhlich", "froh"], []);

        Assert.Equal(AnswerResult.Correct, Check("froh", Languages.English, [.. AnswerChecker.ValidAnswers(card, true)]));
        Assert.Equal(AnswerResult.Correct, Check("happy", Languages.English, [.. AnswerChecker.ValidAnswers(card, false)]));
    }
}
