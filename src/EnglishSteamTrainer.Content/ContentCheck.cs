namespace EnglishSteamTrainer.Core;

public enum ProblemSeverity
{
    Error,
    Warning
}

/// <param name="File">Datei in content/, z. B. "es/vocabulary.csv".</param>
public sealed record ContentProblem(ProblemSeverity Severity, string File, string Message)
{
    public static ContentProblem Error(string file, string message) => new(ProblemSeverity.Error, file, message);
    public static ContentProblem Warning(string file, string message) => new(ProblemSeverity.Warning, file, message);

    public override string ToString() => $"{(Severity == ProblemSeverity.Error ? "Fehler" : "Hinweis")} in {File}: {Message}";
}

/// <summary>
/// Prüft alle Inhalte, bevor sie gespeichert werden (Pflege-Oberfläche) bzw. nach dem Push (CI).
/// Fehler = die Datei würde nicht oder anders gelesen als gedacht. Hinweise = wahrscheinlich ein Versehen.
/// </summary>
public static class ContentCheck
{
    // ";" trennt die Spalten, "|" die Alternativen - beides darf in keinem einzelnen Wert vorkommen.
    private static readonly char[] Forbidden = [';', '|', '\r', '\n'];

    public static List<ContentProblem> Check(ContentSet set)
    {
        var problems = new List<ContentProblem>();

        foreach (var language in Languages.All)
        {
            var content = set[language];

            CheckVocabulary(ContentFiles.Vocabulary(language), content.Vocabulary, problems);
            CheckGrammar(ContentFiles.Grammar(language), content.Grammar, problems);

            if (language.HasVerbForms)
                CheckVerbs(ContentFiles.VerbForms(language), content.Verbs, problems);
        }

        CheckPlan(set, problems);

        return problems;
    }

    private static void CheckVocabulary(string file, List<VocabularyCard> cards, List<ContentProblem> problems)
    {
        if (cards.Count == 0)
            problems.Add(ContentProblem.Error(file, "Es gibt keine einzige Vokabel - die PCs würden die Datei nicht übernehmen."));

        foreach (var card in cards)
        {
            var name = $"\"{card.Word}\"";

            Required(file, name, "Wort", card.Word, problems);
            Required(file, name, "Deutsch", card.German, problems);
            Value(file, name, "Wort", card.Word, problems);
            Value(file, name, "Deutsch", card.German, problems);
            Value(file, name, "Tipp", card.Hint, problems);
            Value(file, name, "Id", card.Id, problems);
            Values(file, name, "Deutsche Alternativen", card.GermanAlternatives, problems);
            Values(file, name, "Alternativen", card.WordAlternatives, problems);
            Values(file, name, "Themen", card.Topics, problems);

            if (card.Word.TrimStart().StartsWith('#'))
                problems.Add(ContentProblem.Error(file, $"{name}: Ein Wort darf nicht mit # beginnen (gilt als Kommentar)."));
        }

        Duplicates(file, cards.Select(card => card.Key), "Wort (bzw. Id)", problems);

        // Ein umbenanntes Wort behält seinen alten Text als Id - der darf nicht das Wort einer anderen Karte sein.
        var words = cards.Where(card => string.IsNullOrWhiteSpace(card.Id)).Select(card => card.Word).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var card in cards.Where(card => !string.IsNullOrWhiteSpace(card.Id) && words.Contains(card.Id!)))
            problems.Add(ContentProblem.Error(file, $"\"{card.Word}\": Id \"{card.Id}\" ist zugleich ein anderes Wort - der Lernverlauf wäre nicht eindeutig."));
    }

    private static void CheckGrammar(string file, List<TenseQuestion> questions, List<ContentProblem> problems)
    {
        if (questions.Count == 0)
            problems.Add(ContentProblem.Error(file, "Es gibt keine einzige Grammatik-Frage - die PCs würden die Datei nicht übernehmen."));

        foreach (var question in questions)
        {
            var name = $"\"{question.Key}\"";

            Required(file, name, "Subjekt", question.Subject, problems);
            Required(file, name, "Verb", question.VerbBase, problems);
            Required(file, name, "Richtige Form", question.CorrectForm, problems);
            Value(file, name, "Subjekt", question.Subject, problems);
            Value(file, name, "Verb", question.VerbBase, problems);
            Value(file, name, "Richtige Form", question.CorrectForm, problems);
            Value(file, name, "Thema", question.Hint, problems);
            Values(file, name, "Falsche Antworten", question.Distractors, problems);

            var distractors = question.Distractors.Select(d => d.Trim()).Where(d => d.Length > 0).ToList();

            if (distractors.Count != TenseQuestionRepository.DistractorCount
                || distractors.Distinct().Count() != distractors.Count
                || distractors.Contains(question.CorrectForm.Trim()))
                problems.Add(ContentProblem.Error(file,
                    $"{name}: Es braucht genau {TenseQuestionRepository.DistractorCount} verschiedene falsche Antworten, die nicht der richtigen entsprechen."));
        }

        Duplicates(file, questions.Select(question => question.Key), "Frage (Subjekt / Verb)", problems);
    }

    private static void CheckVerbs(string file, List<VerbForms> verbs, List<ContentProblem> problems)
    {
        if (verbs.Count == 0)
            problems.Add(ContentProblem.Error(file, "Es gibt kein einziges Verb - die PCs würden die Datei nicht übernehmen."));

        foreach (var verb in verbs)
        {
            var name = $"\"{(verb.Infinitive.Count > 0 ? verb.Key : "?")}\"";

            for (var form = 0; form < VerbForms.FormNames.Length; form++)
            {
                if (verb.All[form].All(string.IsNullOrWhiteSpace))
                    problems.Add(ContentProblem.Error(file, $"{name}: {VerbForms.FormNames[form]} fehlt."));

                Values(file, name, VerbForms.FormNames[form], verb.All[form], problems);
            }

            Required(file, name, "Deutsch", verb.German, problems);
            Value(file, name, "Deutsch", verb.German, problems);
            Values(file, name, "Themen", verb.Topics, problems);
        }

        Duplicates(file, verbs.Where(verb => verb.Infinitive.Count > 0).Select(verb => verb.Key), "Verb", problems);
    }

    private static void CheckPlan(ContentSet set, List<ContentProblem> problems)
    {
        const string file = ContentFiles.Plan;
        var accounts = set.Config.Users.Select(user => user.Account).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entries = set.Plan.Schedule;

        foreach (var entry in entries)
        {
            var name = $"{entry.From:dd.MM.yyyy}–{entry.To:dd.MM.yyyy} \"{entry.DisplayName}\"";
            var language = Languages.Find(entry.Language);

            if (entry.From == default || entry.To == default)
                problems.Add(ContentProblem.Error(file, $"{name}: Von und Bis müssen gesetzt sein."));
            else if (entry.To < entry.From)
                problems.Add(ContentProblem.Error(file, $"{name}: Bis liegt vor Von."));

            if (!string.IsNullOrWhiteSpace(entry.Language) && language is null)
                problems.Add(ContentProblem.Error(file, $"{name}: Unbekannte Sprache \"{entry.Language}\"."));

            if (entry.Topics.Count == 0 || entry.Topics.Any(string.IsNullOrWhiteSpace))
                problems.Add(ContentProblem.Error(file, $"{name}: Mindestens ein Thema, keine leeren Themen."));

            foreach (var account in entry.Accounts ?? [])
            {
                if (string.IsNullOrWhiteSpace(account))
                    problems.Add(ContentProblem.Error(file, $"{name}: Leerer Kontoname."));
                else if (!accounts.Contains(account.Trim()))
                    problems.Add(ContentProblem.Warning(file, $"{name}: Konto \"{account}\" steht nicht in config.json (users) - Tippfehler?"));
            }

            // Für jede Sprache, in der der Eintrag gilt, muss es zu jedem Thema Aufgaben geben.
            foreach (var lang in TargetLanguages(set, entry))
            {
                foreach (var topic in entry.Topics.Where(topic => !string.IsNullOrWhiteSpace(topic)))
                {
                    if (set[lang].CountTopic(topic) == 0)
                        problems.Add(ContentProblem.Error(file, $"{name}: Zum Thema \"{topic}\" gibt es auf {lang.Name} keine Aufgaben."));
                }
            }
        }

        // Überschneidungen bei gleicher Zielgruppe: dann gilt nur einer der Einträge.
        for (var i = 0; i < entries.Count; i++)
        {
            for (var j = i + 1; j < entries.Count; j++)
            {
                var a = entries[i];
                var b = entries[j];

                if (a.From <= b.To && b.From <= a.To && SameAudience(a, b))
                    problems.Add(ContentProblem.Warning(file,
                        $"\"{a.DisplayName}\" ({a.From:dd.MM.}–{a.To:dd.MM.}) und \"{b.DisplayName}\" ({b.From:dd.MM.}–{b.To:dd.MM.}) überschneiden sich - es gilt nur der erste."));
            }
        }

        foreach (var focus in set.Config.FocusTopics)
        {
            var languages = Languages.Find(focus.Language) is { } lang ? [lang] : Languages.All;

            foreach (var language in languages.Where(language => !set[language].Vocabulary.Any(card => card.HasTopic(focus.Topic))))
                problems.Add(ContentProblem.Error(ContentFiles.Config,
                    $"focusTopics: Thema \"{focus.Topic}\" ({focus.From:dd.MM.yyyy}) hat keine Vokabeln auf {language.Name}."));
        }
    }

    // Sprachen, für die ein Eintrag gilt: angegebene Sprache, sonst die der genannten Kinder, sonst alle.
    private static IEnumerable<LearningLanguage> TargetLanguages(ContentSet set, PlanEntry entry)
    {
        if (Languages.Find(entry.Language) is { } language)
            return [language];

        if (!string.IsNullOrWhiteSpace(entry.Language))
            return [];

        if (entry.Accounts is { Count: > 0 })
        {
            var languages = entry.Accounts
                .SelectMany(account => set.Config.Users.Where(user => string.Equals(user.Account, account.Trim(), StringComparison.OrdinalIgnoreCase)))
                .Select(user => Languages.Find(user.Language))
                .ToList();

            // Kind ohne zentrale Sprache: Sprache steht nur auf dem PC, also alle prüfen.
            if (languages.Count > 0 && languages.All(l => l is not null))
                return languages.Distinct()!;
        }

        return Languages.All;
    }

    private static bool SameAudience(PlanEntry a, PlanEntry b)
    {
        static HashSet<string> Accounts(PlanEntry entry) =>
            (entry.Accounts ?? []).Select(account => account.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return string.Equals(a.Language?.Trim() ?? "", b.Language?.Trim() ?? "", StringComparison.OrdinalIgnoreCase)
               && Accounts(a).SetEquals(Accounts(b));
    }

    private static void Required(string file, string name, string field, string? value, List<ContentProblem> problems)
    {
        if (string.IsNullOrWhiteSpace(value))
            problems.Add(ContentProblem.Error(file, $"{name}: {field} fehlt."));
    }

    private static void Value(string file, string name, string field, string? value, List<ContentProblem> problems)
    {
        if (value is not null && value.IndexOfAny(Forbidden) >= 0)
            problems.Add(ContentProblem.Error(file, $"{name}: {field} darf weder ; noch | noch Zeilenumbrüche enthalten."));
    }

    private static void Values(string file, string name, string field, IEnumerable<string>? values, List<ContentProblem> problems)
    {
        foreach (var value in values ?? [])
            Value(file, name, field, value, problems);
    }

    private static void Duplicates(string file, IEnumerable<string> keys, string what, List<ContentProblem> problems)
    {
        foreach (var duplicate in keys
                     .Where(key => !string.IsNullOrWhiteSpace(key))
                     .GroupBy(key => key.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
            problems.Add(ContentProblem.Error(file, $"{what} \"{duplicate.Key}\" gibt es {duplicate.Count()}-mal."));
    }
}
