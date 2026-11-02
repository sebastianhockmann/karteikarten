using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnglishSteamTrainer.Core;

/// <summary>
/// Ein Abschnitt im Lernplan: vom <see cref="From"/> bis einschließlich <see cref="To"/>
/// kommen Aufgaben zu diesen <see cref="Topics"/> bevorzugt dran (Vokabeln, Grammatik, Verben).
/// </summary>
public sealed record PlanEntry
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>Optional, wird statt der Themenliste angezeigt ("Unit 3: Party").</summary>
    public string? Title { get; init; }

    /// <summary>Optional, sonst für alle Sprachen.</summary>
    public string? Language { get; init; }

    /// <summary>Optional: Windows-Kontonamen. Leer = alle Kinder (mit passender Sprache).</summary>
    public List<string>? Accounts { get; init; }

    public List<string> Topics { get; init; } = [];

    public bool IsActive(DateOnly date) => From <= date && date <= To;

    public bool AppliesTo(LearningLanguage language, string account) =>
        (string.IsNullOrWhiteSpace(Language) || Languages.Find(Language) == language)
        && (Accounts is not { Count: > 0 }
            || Accounts.Any(a => string.Equals(a.Trim(), account, StringComparison.OrdinalIgnoreCase)));

    public bool HasTopic(string topic) => TopicNames.Contains(Topics, topic);

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? string.Join(", ", Topics) : Title.Trim();

    // Vorrang bei gleichzeitig aktiven Einträgen: für ein Kind vor für eine Sprache vor für alle.
    internal int Specificity =>
        (Accounts is { Count: > 0 } ? 2 : 0) + (string.IsNullOrWhiteSpace(Language) ? 0 : 1);
}

/// <summary>
/// Lernplan (content/plan.json): welche Themen wann dran sind, pro Sprache oder pro Kind.
/// Ersetzt "focusTopics" aus config.json (das gilt weiterhin, siehe <see cref="WithLegacyFocusTopics"/>).
/// </summary>
public sealed class LearningPlan
{
    public const string FileName = "plan.json";

    private static readonly JsonSerializerOptions WriteOptions = new(ContentJson.Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Umlaute lesbar lassen statt ä.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Themen, die im Plan erst später kommen, werden vorher nicht abgefragt. Themen, die
    /// gar nicht im Plan stehen, und Aufgaben ohne Thema sind immer dabei.
    /// </summary>
    public bool HoldBackPlannedTopics { get; set; } = true;

    public List<PlanEntry> Schedule { get; set; } = [];

    /// <summary>Liefert null, wenn die Datei unbrauchbar ist (dann bleibt die letzte gute Version).</summary>
    public static LearningPlan? TryParse(string json)
    {
        try
        {
            var plan = JsonSerializer.Deserialize<LearningPlan>(json, ContentJson.Options);

            if (plan is null)
                return null;

            plan.Schedule ??= [];

            return plan.Schedule.All(IsValid) ? plan : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsValid(PlanEntry? entry) =>
        entry is not null
        && entry.From != default
        && entry.To >= entry.From
        && entry.Topics is { Count: > 0 }
        && entry.Topics.All(topic => !string.IsNullOrWhiteSpace(topic))
        && (string.IsNullOrWhiteSpace(entry.Language) || Languages.Find(entry.Language) is not null)
        && (entry.Accounts is null || entry.Accounts.All(account => !string.IsNullOrWhiteSpace(account)));

    public string ToJson() => JsonSerializer.Serialize(this, WriteOptions).ReplaceLineEndings("\n") + "\n";

    /// <summary>Einträge aus "focusTopics" (config.json, bis Version 1.3) kommen hinten dazu.</summary>
    public LearningPlan WithLegacyFocusTopics(IEnumerable<FocusTopic> focusTopics)
    {
        return new LearningPlan
        {
            HoldBackPlannedTopics = HoldBackPlannedTopics,
            Schedule =
            [
                .. Schedule,
                .. focusTopics.Select(focus => new PlanEntry
                {
                    From = focus.From,
                    To = focus.To,
                    Language = focus.Language,
                    Topics = [focus.Topic]
                })
            ]
        };
    }

    public LearnerPlan For(LearningLanguage language, string account) =>
        new(Schedule.Where(entry => entry.AppliesTo(language, account)).ToList(), HoldBackPlannedTopics);
}

/// <summary>Der Lernplan aus Sicht eines Kindes (nur die Einträge, die für es gelten).</summary>
public sealed class LearnerPlan(IReadOnlyList<PlanEntry> entries, bool holdBackPlannedTopics)
{
    public IReadOnlyList<PlanEntry> Entries { get; } = entries;

    /// <summary>Bei Überschneidung gewinnt der spezifischere, sonst der erste Eintrag in der Datei.</summary>
    public PlanEntry? ActiveEntry(DateOnly date) =>
        Entries
            .Where(entry => entry.IsActive(date))
            .OrderByDescending(entry => entry.Specificity)
            .FirstOrDefault();

    public bool IsHeldBack(IEnumerable<string>? itemTopics, DateOnly date)
    {
        if (!holdBackPlannedTopics || itemTopics is null)
            return false;

        var topics = itemTopics.ToList();

        if (topics.Count == 0)
            return false;

        // Zurückgehalten nur, wenn jedes Thema der Aufgabe geplant ist und noch nicht begonnen hat.
        foreach (var topic in topics)
        {
            var planned = Entries.Where(entry => entry.HasTopic(topic)).ToList();

            if (planned.Count == 0 || planned.Any(entry => entry.From <= date))
                return false;
        }

        return true;
    }
}
