namespace EnglishSteamTrainer.Core;

/// <summary>
/// Stammformen eines Verbs. Jede Form kann mehrere richtige Schreibweisen haben
/// (z. B. "was|were", "got|gotten"); die erste wird angezeigt.
/// </summary>
/// <param name="Topics">Optional, Themen für den <see cref="LearningPlan"/>.</param>
public sealed record VerbForms(
    List<string> Infinitive,
    List<string> SimplePast,
    List<string> PastParticiple,
    string German,
    List<string>? Topics = null
)
{
    public static readonly string[] FormNames = ["Infinitive", "Simple Past", "Past Participle"];

    public string Key => Infinitive[0];

    public IReadOnlyList<List<string>> All => [Infinitive, SimplePast, PastParticiple];

    public string Display(int form) => string.Join(" / ", All[form]);

    public bool HasTopic(string topic) => TopicNames.Contains(Topics, topic);
}

/// <summary>
/// Liest und schreibt verbs.csv: Infinitive;SimplePast;PastParticiple;German;Topics
/// (mehrere richtige Schreibweisen und mehrere Themen mit | getrennt).
/// </summary>
public static class VerbFormsRepository
{
    public const string Header = "Infinitive;SimplePast;PastParticiple;German;Topics";

    public static List<VerbForms> Parse(string csv)
    {
        var verbs = new List<VerbForms>();

        foreach (var line in VocabularyRepository.CsvLines(csv))
        {
            var parts = line.Split(';');

            if (parts.Length < 4)
                continue;

            var infinitive = VocabularyRepository.SplitAlternatives(parts, 0);
            var simplePast = VocabularyRepository.SplitAlternatives(parts, 1);
            var pastParticiple = VocabularyRepository.SplitAlternatives(parts, 2);
            var german = parts[3].Trim();

            if (infinitive.Count == 0 || simplePast.Count == 0 || pastParticiple.Count == 0 || german.Length == 0)
                continue;

            verbs.Add(new VerbForms(infinitive, simplePast, pastParticiple, german, VocabularyRepository.SplitAlternatives(parts, 4)));
        }

        return verbs;
    }

    public static string Format(IEnumerable<VerbForms> verbs)
    {
        return VocabularyRepository.FormatCsv(Header, verbs.Select(verb => new[]
        {
            VocabularyRepository.JoinAlternatives(verb.Infinitive),
            VocabularyRepository.JoinAlternatives(verb.SimplePast),
            VocabularyRepository.JoinAlternatives(verb.PastParticiple),
            verb.German,
            VocabularyRepository.JoinAlternatives(verb.Topics)
        }));
    }
}
