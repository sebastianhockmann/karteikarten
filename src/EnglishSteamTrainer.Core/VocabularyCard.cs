namespace EnglishSteamTrainer.Core;

public sealed record VocabularyCard(
    string English,
    string German,
    string Hint,
    List<string> Alternatives
);