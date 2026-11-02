namespace EnglishSteamTrainer.Core;

/// <summary>
/// Liest und schreibt vocabulary.csv: Word;German;Hint;GermanAlternatives;WordAlternatives;Topics;Id
/// (erste Zeile = Überschrift, Alternativen und Themen mit | getrennt, Id meist leer).
/// </summary>
public static class VocabularyRepository
{
    public const string Header = "Word;German;Hint;GermanAlternatives;WordAlternatives;Topics;Id";

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
                SplitAlternatives(parts, 4),
                SplitAlternatives(parts, 5),
                parts.Length >= 7 && parts[6].Trim().Length > 0 ? parts[6].Trim() : null));
        }

        return cards;
    }

    public static string Format(IEnumerable<VocabularyCard> cards)
    {
        return FormatCsv(Header, cards.Select(card => new[]
        {
            card.Word,
            card.German,
            card.Hint,
            JoinAlternatives(card.GermanAlternatives),
            JoinAlternatives(card.WordAlternatives),
            JoinAlternatives(card.Topics),
            card.Id ?? ""
        }));
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

    internal static string JoinAlternatives(IEnumerable<string>? values) =>
        values is null ? "" : string.Join('|', values.Select(value => value.Trim()).Where(value => value.Length > 0));

    // Leere Spalten am Zeilenende fallen weg, so bleiben die Dateien so knapp wie von Hand
    // geschrieben. LF wie im Repo (Git wandelt unter Windows lokal in CRLF um).
    internal static string FormatCsv(string header, IEnumerable<string[]> rows)
    {
        var lines = rows.Select(row =>
        {
            var fields = row.Select(field => field.Trim()).ToList();

            while (fields.Count > 2 && fields[^1].Length == 0)
                fields.RemoveAt(fields.Count - 1);

            return string.Join(';', fields);
        });

        return string.Join("\n", lines.Prepend(header)) + "\n";
    }
}
