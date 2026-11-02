namespace EnglishSteamTrainer.Core;

/// <summary>Die Dateien in content/ (relativ zum Ordner, mit "/" getrennt).</summary>
public static class ContentFiles
{
    public const string Config = "config.json";
    public const string Plan = LearningPlan.FileName;

    public static string Vocabulary(LearningLanguage language) => $"{language.Code}/vocabulary.csv";
    public static string Grammar(LearningLanguage language) => $"{language.Code}/grammar.csv";
    public static string VerbForms(LearningLanguage language) => $"{language.Code}/verbs.csv";

    public static IEnumerable<string> All =>
        Languages.All
            .SelectMany(language => language.HasVerbForms
                ? new[] { Vocabulary(language), Grammar(language), VerbForms(language) }
                : new[] { Vocabulary(language), Grammar(language) })
            .Prepend(Plan)
            .Prepend(Config);

    /// <summary>
    /// Gleiche Prüfung beim Herunterladen wie beim Laden: Unbrauchbares landet gar
    /// nicht erst im Cache, die letzte gute Version bleibt erhalten.
    /// </summary>
    public static bool IsValid(string relativePath, string text)
    {
        if (relativePath == Config)
            return TrainerConfig.TryParse(text) is not null;

        if (relativePath == Plan)
            return LearningPlan.TryParse(text) is not null;

        if (relativePath.EndsWith("/vocabulary.csv", StringComparison.Ordinal))
            return VocabularyRepository.Parse(text).Count > 0;

        if (relativePath.EndsWith("/grammar.csv", StringComparison.Ordinal))
            return TenseQuestionRepository.Parse(text).Count > 0;

        if (relativePath.EndsWith("/verbs.csv", StringComparison.Ordinal))
            return VerbFormsRepository.Parse(text).Count > 0;

        return false;
    }
}
