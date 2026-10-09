namespace EnglishSteamTrainer.Core;

/// <param name="Word">Wort in der Lernsprache (Englisch, Spanisch, ...).</param>
public sealed record VocabularyCard(
    string Word,
    string German,
    string Hint,
    List<string> GermanAlternatives,
    List<string> WordAlternatives
);
