namespace EnglishSteamTrainer.Core;

public enum ContentSource
{
    OnlineCache,
    Bundled,
    BuiltIn
}

public sealed record LoadedContent<T>(T Value, ContentSource Source);

/// <summary>
/// Vokabeln, Grammatik, Lernplan und config.json kommen aus dem Ordner content/ im GitHub-Repo.
/// Reihenfolge beim Laden: zuletzt online geladene Version (Cache, nur von SYSTEM/Admins
/// beschreibbar) -> mit der App ausgelieferte Version -> eingebaute Standardwerte.
/// </summary>
public static class ContentStore
{
    public static string CacheRoot => Path.Combine(DailyUnlock.SharedFolder, "cache");
    public static string CacheFolder => Path.Combine(CacheRoot, "content");
    public static string LastSyncFile => Path.Combine(CacheRoot, "last-sync.txt");
    public static string BundledFolder => Path.Combine(AppContext.BaseDirectory, "content");

    public static LoadedContent<TrainerConfig> LoadConfig()
    {
        return Load(ContentFiles.Config, TrainerConfig.TryParse)
               ?? new LoadedContent<TrainerConfig>(TrainerConfig.Default, ContentSource.BuiltIn);
    }

    /// <summary>Ohne plan.json (z. B. vor dem ersten Abgleich) gilt ein leerer Plan.</summary>
    public static LoadedContent<LearningPlan> LoadPlan()
    {
        return Load(ContentFiles.Plan, LearningPlan.TryParse)
               ?? new LoadedContent<LearningPlan>(new LearningPlan(), ContentSource.BuiltIn);
    }

    public static LoadedContent<List<VocabularyCard>> LoadVocabulary(LearningLanguage language)
    {
        return Load(ContentFiles.Vocabulary(language), text => NonEmpty(VocabularyRepository.Parse(text)))
               ?? new LoadedContent<List<VocabularyCard>>([], ContentSource.BuiltIn);
    }

    public static LoadedContent<List<TenseQuestion>> LoadGrammar(LearningLanguage language)
    {
        return Load(ContentFiles.Grammar(language), text => NonEmpty(TenseQuestionRepository.Parse(text)))
               ?? new LoadedContent<List<TenseQuestion>>([], ContentSource.BuiltIn);
    }

    public static LoadedContent<List<VerbForms>> LoadVerbForms(LearningLanguage language)
    {
        if (!language.HasVerbForms)
            return new LoadedContent<List<VerbForms>>([], ContentSource.BuiltIn);

        return Load(ContentFiles.VerbForms(language), text => NonEmpty(VerbFormsRepository.Parse(text)))
               ?? new LoadedContent<List<VerbForms>>([], ContentSource.BuiltIn);
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
