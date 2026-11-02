namespace EnglishSteamTrainer.Core;

/// <param name="Word">Wort in der Lernsprache (Englisch, Spanisch, ...).</param>
/// <param name="Topics">Themen, z. B. "Tiere" oder "Party-Essen" (für den <see cref="LearningPlan"/>).</param>
/// <param name="Id">
/// Nur nötig, wenn ein Wort umbenannt wurde: dann steht hier der alte Text, damit der
/// Lernverlauf (Spalte "Aufgabe" im Lernjournal) weiter zum Wort gehört.
/// </param>
public sealed record VocabularyCard(
    string Word,
    string German,
    string Hint,
    List<string> GermanAlternatives,
    List<string> WordAlternatives,
    List<string>? Topics = null,
    string? Id = null
)
{
    /// <summary>Schlüssel im Lernjournal.</summary>
    public string Key => string.IsNullOrWhiteSpace(Id) ? Word : Id;

    public bool HasTopic(string topic) => TopicNames.Contains(Topics, topic);
}
