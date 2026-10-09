using System.Globalization;
using System.Text;

namespace EnglishSteamTrainer.Core;

public enum AnswerResult
{
    Correct,
    Typo,
    Wrong
}

/// <param name="Expected">Die passende richtige Schreibweise (zum Anzeigen).</param>
public sealed record AnswerCheck(AnswerResult Result, string Expected);

public static class AnswerChecker
{
    // Ein Tippfehler wird erst ab dieser Wortlänge verziehen - sonst wäre z. B.
    // "Mund" eine richtige Antwort für "Hund".
    public const int MinLengthForTypo = 5;

    public static List<string> ValidAnswers(VocabularyCard card, bool askWordToGerman)
    {
        return askWordToGerman
            ? [card.German, .. card.GermanAlternatives]
            : [card.Word, .. card.WordAlternatives];
    }

    public static AnswerCheck Check(string given, IReadOnlyList<string> validAnswers, LearningLanguage language)
    {
        var articles = Languages.GermanArticles.Concat(language.Articles).ToArray();
        var answer = Normalize(given, articles);

        if (answer.Length == 0 || validAnswers.Count == 0)
            return new AnswerCheck(AnswerResult.Wrong, validAnswers.FirstOrDefault() ?? "");

        foreach (var valid in validAnswers)
        {
            if (Normalize(valid, articles) == answer)
                return new AnswerCheck(AnswerResult.Correct, valid);
        }

        foreach (var valid in validAnswers)
        {
            var normalized = Normalize(valid, articles);

            if (normalized.Length >= MinLengthForTypo && LevenshteinDistance(answer, normalized) <= 1)
                return new AnswerCheck(AnswerResult.Typo, valid);
        }

        return new AnswerCheck(AnswerResult.Wrong, validAnswers[0]);
    }

    /// <summary>
    /// Groß-/Kleinschreibung, Akzente (é, ñ), Umlaute (ä = ae) und ein führender
    /// Artikel spielen beim Vergleich keine Rolle.
    /// </summary>
    public static string Normalize(string value, IReadOnlyCollection<string> articles)
    {
        var text = value.Trim()
            .ToLowerInvariant()
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");

        var builder = new StringBuilder(text.Length);

        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        var words = builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (words.Count > 1 && articles.Contains(words[0]))
            words.RemoveAt(0);

        return string.Join(' ', words);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var matrix = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++)
            matrix[i, 0] = i;

        for (var j = 0; j <= b.Length; j++)
            matrix[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[a.Length, b.Length];
    }
}
