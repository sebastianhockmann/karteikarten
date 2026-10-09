namespace EnglishSteamTrainer.Core;

public enum ContentSource
{
    OnlineCache,
    Bundled,
    BuiltIn
}

public sealed record LoadedContent<T>(T Value, ContentSource Source);

/// <summary>
/// Vokabeln, Grammatik und config.json kommen aus dem Ordner content/ im GitHub-Repo.
/// Reihenfolge beim Laden: zuletzt online geladene Version (Cache, nur von SYSTEM/Admins
/// beschreibbar) -> mit der App ausgelieferte Version -> eingebaute Standardwerte.
/// </summary>
public static class ContentStore
{
    public const string ConfigFile = "config.json";

    public static string CacheRoot => Path.Combine(DailyUnlock.SharedFolder, "cache");
    public static string CacheFolder => Path.Combine(CacheRoot, "content");
    public static string LastSyncFile => Path.Combine(CacheRoot, "last-sync.txt");
    public static string BundledFolder => Path.Combine(AppContext.BaseDirectory, "content");

    public static string VocabularyFile(LearningLanguage language) => $"{language.Code}/vocabulary.csv";
    public static string GrammarFile(LearningLanguage language) => $"{language.Code}/grammar.csv";

    public static IEnumerable<string> AllFiles =>
        Languages.All
            .SelectMany(language => new[] { VocabularyFile(language), GrammarFile(language) })
            .Prepend(ConfigFile);

    public static LoadedContent<TrainerConfig> LoadConfig()
    {
        return Load(ConfigFile, TrainerConfig.TryParse)
               ?? new LoadedContent<TrainerConfig>(TrainerConfig.Default, ContentSource.BuiltIn);
    }

    public static LoadedContent<List<VocabularyCard>> LoadVocabulary(LearningLanguage language)
    {
        return Load(VocabularyFile(language), text => NonEmpty(VocabularyRepository.Parse(text)))
               ?? new LoadedContent<List<VocabularyCard>>([], ContentSource.BuiltIn);
    }

    public static LoadedContent<List<TenseQuestion>> LoadGrammar(LearningLanguage language)
    {
        return Load(GrammarFile(language), text => NonEmpty(TenseQuestionRepository.Parse(text)))
               ?? new LoadedContent<List<TenseQuestion>>([], ContentSource.BuiltIn);
    }

    /// <summary>
    /// Gleiche Prüfung beim Herunterladen wie beim Laden: Unbrauchbares landet gar
    /// nicht erst im Cache, die letzte gute Version bleibt erhalten.
    /// </summary>
    public static bool IsValid(string relativePath, string text)
    {
        if (relativePath == ConfigFile)
            return TrainerConfig.TryParse(text) is not null;

        if (relativePath.EndsWith("/vocabulary.csv", StringComparison.Ordinal))
            return VocabularyRepository.Parse(text).Count > 0;

        if (relativePath.EndsWith("/grammar.csv", StringComparison.Ordinal))
            return TenseQuestionRepository.Parse(text).Count > 0;

        return false;
    }

    public static DateTime? GetLastSync()
    {
        try
        {
            return File.Exists(LastSyncFile) ? File.GetLastWriteTime(LastSyncFile) : null;
        }
        catch
        {
            return null;
        }
    }

    private static LoadedContent<T>? Load<T>(string relativePath, Func<string, T?> parse)
        where T : class
    {
        foreach (var (folder, source) in new[]
                 {
                     (CacheFolder, ContentSource.OnlineCache),
                     (BundledFolder, ContentSource.Bundled)
                 })
        {
            try
            {
                var path = Path.Combine(folder, relativePath);

                if (!File.Exists(path))
                    continue;

                var value = parse(File.ReadAllText(path));

                if (value is not null)
                    return new LoadedContent<T>(value, source);
            }
            catch
            {
                // Nicht lesbar - nächste Quelle versuchen.
            }
        }

        return null;
    }

    private static List<T>? NonEmpty<T>(List<T> items) => items.Count > 0 ? items : null;
}
