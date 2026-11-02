namespace EnglishSteamTrainer.Core;

public sealed class LanguageContent
{
    public List<VocabularyCard> Vocabulary { get; set; } = [];
    public List<TenseQuestion> Grammar { get; set; } = [];
    public List<VerbForms> Verbs { get; set; } = [];

    public QuizContent ToQuiz() => new(Vocabulary, Grammar, Verbs);

    /// <summary>Alle Themen aus Vokabeln, Grammatik und Verben, ohne Doppelte.</summary>
    public IEnumerable<string> Topics =>
        Vocabulary.SelectMany(card => card.Topics ?? [])
            .Concat(Grammar.Select(question => question.Hint))
            .Concat(Verbs.SelectMany(verb => verb.Topics ?? []))
            .Distinct(TopicNames.Comparer);

    public int CountTopic(string topic) =>
        Vocabulary.Count(card => card.HasTopic(topic))
        + Grammar.Count(question => question.HasTopic(topic))
        + Verbs.Count(verb => verb.HasTopic(topic));
}

/// <summary>
/// Der ganze Ordner content/ im Speicher - für die Pflege-Oberfläche und die Prüfung in CI.
/// config.json wird nur gelesen, nie neu geschrieben (Reihenfolge und Format bleiben so wie von Hand).
/// </summary>
public sealed class ContentSet
{
    public TrainerConfig Config { get; set; } = TrainerConfig.Default;
    public LearningPlan Plan { get; set; } = new();
    public Dictionary<string, LanguageContent> Languages { get; } = new(StringComparer.OrdinalIgnoreCase);

    public LanguageContent this[LearningLanguage language] =>
        Languages.TryGetValue(language.Code, out var content) ? content : Languages[language.Code] = new LanguageContent();

    /// <param name="readFile">Liefert den Inhalt einer Datei aus <see cref="ContentFiles.All"/> oder null, wenn es sie nicht gibt.</param>
    /// <param name="problems">Hier landen Dateien und Zeilen, die nicht gelesen werden konnten.</param>
    public static ContentSet Parse(Func<string, string?> readFile, List<ContentProblem> problems)
    {
        var set = new ContentSet();

        var configText = readFile(ContentFiles.Config);

        if (configText is null)
            problems.Add(ContentProblem.Error(ContentFiles.Config, "Datei fehlt."));
        else if (TrainerConfig.TryParse(configText) is { } config)
            set.Config = config;
        else
            problems.Add(ContentProblem.Error(ContentFiles.Config, "Datei ist ungültig (JSON, Sprache, Zahlenbereich oder leere Sperrliste)."));

        var planText = readFile(ContentFiles.Plan);

        if (planText is not null)
        {
            if (LearningPlan.TryParse(planText) is { } plan)
                set.Plan = plan;
            else
                problems.Add(ContentProblem.Error(ContentFiles.Plan, "Datei ist ungültig (JSON, Datum im Format JJJJ-MM-TT, Themen, Sprache)."));
        }

        foreach (var language in EnglishSteamTrainer.Core.Languages.All)
        {
            var content = set[language];

            content.Vocabulary = ParseCsv(readFile, ContentFiles.Vocabulary(language), VocabularyRepository.Parse, problems);
            content.Grammar = ParseCsv(readFile, ContentFiles.Grammar(language), TenseQuestionRepository.Parse, problems);

            if (language.HasVerbForms)
                content.Verbs = ParseCsv(readFile, ContentFiles.VerbForms(language), VerbFormsRepository.Parse, problems);
        }

        return set;
    }

    /// <summary>Die Dateien, die die Oberfläche schreibt (alles außer config.json).</summary>
    public Dictionary<string, string> FormatFiles()
    {
        var files = new Dictionary<string, string> { [ContentFiles.Plan] = Plan.ToJson() };

        foreach (var language in EnglishSteamTrainer.Core.Languages.All)
        {
            var content = this[language];

            files[ContentFiles.Vocabulary(language)] = VocabularyRepository.Format(content.Vocabulary);
            files[ContentFiles.Grammar(language)] = TenseQuestionRepository.Format(content.Grammar);

            if (language.HasVerbForms)
                files[ContentFiles.VerbForms(language)] = VerbFormsRepository.Format(content.Verbs);
        }

        return files;
    }

    // Jede Datenzeile einzeln, damit klar ist, welche Zeile nicht gelesen wird - sie ginge
    // beim Speichern aus der Oberfläche sonst verloren.
    private static List<T> ParseCsv<T>(
        Func<string, string?> readFile,
        string path,
        Func<string, List<T>> parse,
        List<ContentProblem> problems)
    {
        var text = readFile(path);

        if (text is null)
        {
            problems.Add(ContentProblem.Error(path, "Datei fehlt."));
            return [];
        }

        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var header = lines.FirstOrDefault() ?? "";

        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.TrimStart().StartsWith('#'))
            {
                problems.Add(ContentProblem.Warning(path, $"Zeile {i + 1} ist ein Kommentar und fällt beim Speichern aus der Oberfläche weg: {line}"));
                continue;
            }

            if (parse(header + "\n" + line).Count == 0)
                problems.Add(ContentProblem.Error(path, $"Zeile {i + 1} wird nicht gelesen (Spalten oder Pflichtfelder prüfen): {line}"));
        }

        return parse(text);
    }
}
