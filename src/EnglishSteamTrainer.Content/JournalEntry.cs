namespace EnglishSteamTrainer.Core;

public static class JournalKind
{
    public const string Vocabulary = "vocab";
    public const string Grammar = "grammar";
    public const string VerbForms = "verbs";
    public const string Import = "import";
}

public sealed record JournalEntry(
    int Sequence,
    DateTime Time,
    string Language,
    string Kind,
    string Item,
    string Prompt,
    string Given,
    AnswerResult? Result,
    int Xp)
{
    public bool CountsAsCorrect => Result is AnswerResult.Correct or AnswerResult.Typo;
}
