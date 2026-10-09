namespace EnglishSteamTrainer.Core;

public sealed record TenseQuestion(
    string Subject,
    string VerbBase,
    string CorrectForm,
    List<string> Distractors,
    string Hint
)
{
    public string Key => $"{Subject} / {VerbBase}";
}
