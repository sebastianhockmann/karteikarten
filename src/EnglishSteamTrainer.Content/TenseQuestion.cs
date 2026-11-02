namespace EnglishSteamTrainer.Core;

/// <param name="Hint">Spalte Topic: wird bei der Frage angezeigt und ist zugleich das Thema im Lernplan.</param>
public sealed record TenseQuestion(
    string Subject,
    string VerbBase,
    string CorrectForm,
    List<string> Distractors,
    string Hint
)
{
    public string Key => $"{Subject} / {VerbBase}";

    public bool HasTopic(string topic) => TopicNames.Same(Hint, topic);
}
