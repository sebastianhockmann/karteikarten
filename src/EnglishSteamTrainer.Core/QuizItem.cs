namespace EnglishSteamTrainer.Core;

public abstract record QuizItem;

public sealed record VocabQuizItem(
    VocabularyCard Card,
    bool AskWordToGerman
) : QuizItem;

public sealed record TenseQuizItem(
    TenseQuestion Question,
    List<string> ShuffledOptions
) : QuizItem;
