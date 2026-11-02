namespace EnglishSteamTrainer.Core;

public abstract record QuizItem;

public sealed record VocabQuizItem(
    VocabularyCard Card,
    bool AskWordToGerman
) : QuizItem;

/// <param name="GivenForm">Index in <see cref="VerbForms.All"/>, diese Form wird vorgegeben.</param>
public sealed record VerbFormsQuizItem(
    VerbForms Verb,
    int GivenForm
) : QuizItem;

public sealed record TenseQuizItem(
    TenseQuestion Question,
    List<string> ShuffledOptions
) : QuizItem;
