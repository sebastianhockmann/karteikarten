using System.Security.Principal;

namespace EnglishSteamTrainer.Core;

/// <param name="Language">"--language es": Sprache unabhängig von der Zuordnung.</param>
/// <param name="Only">"--only verbs|grammar|vocab": nur diese Art von Aufgaben.</param>
public sealed record TestOptions(LearningLanguage? Language, string? Only);

/// <summary>
/// "--test" für Entwickler und Eltern: Fenster lässt sich jederzeit schließen, kein
/// Watchdog, Antworten gehen in ein Wegwerf-Journal, nichts wird freigeschaltet.
/// Nur für Konten, die nicht gesperrt sind - die Liste in settings.json kann das
/// Kind nicht ändern, also auch den Testmodus nicht für sich einschalten.
/// </summary>
public static class TestMode
{
    public static readonly string[] OnlyKinds = [JournalKind.Vocabulary, JournalKind.Grammar, JournalKind.VerbForms];

    public static bool IsRequested(string[] args) => args.Any(arg => arg is "--test" or "-test");

    public static bool IsAllowed(InstallSettings settings, SecurityIdentifier user) => settings.FindUser(user) is null;

    public static TestOptions Parse(string[] args)
    {
        var language = Languages.Find(ValueAfter(args, "--language"));
        var only = ValueAfter(args, "--only")?.Trim().ToLowerInvariant();

        return new TestOptions(language, only is not null && OnlyKinds.Contains(only) ? only : null);
    }

    internal static string? ValueAfter(string[] args, string name)
    {
        var index = Array.FindIndex(args, arg => arg == name || arg == name[1..]);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
