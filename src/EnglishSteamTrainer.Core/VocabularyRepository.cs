namespace EnglishSteamTrainer.Core;

/// <summary>
/// Liest vocabulary.csv: Word;German;Hint;GermanAlternatives;WordAlternatives
/// (erste Zeile = Überschrift, Alternativen mit | getrennt).
/// </summary>
public static class VocabularyRepository
{
    public static List<VocabularyCard> Parse(string csv)
    {
        var cards = new List<VocabularyCard>();

        foreach (var line in CsvLines(csv))
        {
            var parts = line.Split(';');

            if (parts.Length < 2
                || string.IsNullOrWhiteSpace(parts[0])
                || string.IsNullOrWhiteSpace(parts[1]))
                continue;

            cards.Add(new VocabularyCard(
                parts[0].Trim(),
                parts[1].Trim(),
                parts.Length >= 3 ? parts[2].Trim() : "",
                SplitAlternatives(parts, 3),
                SplitAlternatives(parts, 4)));
        }

        return cards;
    }

    internal static IEnumerable<string> CsvLines(string csv)
    {
        return csv
            .Split('\n')
            .Skip(1)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'));
    }

    internal static List<string> SplitAlternatives(string[] parts, int index)
    {
        if (parts.Length <= index)
            return [];

        return parts[index]
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }
}
