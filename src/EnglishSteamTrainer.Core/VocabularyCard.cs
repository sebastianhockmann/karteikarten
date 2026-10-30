namespace EnglishSteamTrainer.Core;

/// <param name="Word">Wort in der Lernsprache (Englisch, Spanisch, ...).</param>
/// <param name="Topics">Themen, z. B. "Tiere" oder "Party-Essen" (für <see cref="FocusTopic"/>).</param>
public sealed record VocabularyCard(
    string Word,
    string German,
    string Hint,
    List<string> GermanAlternatives,
    List<string> WordAlternatives,
    List<string>? Topics = null
)
{
    public bool HasTopic(string topic) =>
        Topics?.Any(t => string.Equals(t, topic.Trim(), StringComparison.OrdinalIgnoreCase)) == true;
}
