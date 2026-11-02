namespace EnglishSteamTrainer.Core;

/// <summary>Themen werden ohne Rücksicht auf Groß-/Kleinschreibung und Leerzeichen am Rand verglichen.</summary>
public static class TopicNames
{
    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public static bool Same(string? a, string? b) => Comparer.Equals(a?.Trim() ?? "", b?.Trim() ?? "");

    public static bool Contains(IEnumerable<string>? topics, string topic) =>
        topics?.Any(t => Same(t, topic)) == true;
}
