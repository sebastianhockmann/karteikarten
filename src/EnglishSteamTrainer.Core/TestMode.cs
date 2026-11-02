using System.Security.Principal;

namespace EnglishSteamTrainer.Core;

/// <param name="Language">"--language es": Sprache unabhängig von der Zuordnung.</param>
/// <param name="Only">"--only verbs|grammar|vocab": nur diese Art von Aufgaben.</param>
public sealed record TestOptions(LearningLanguage? Language, string? Only);

/// <summary>
/// Entwicklermodus (früher "--test"): gilt automatisch für jedes Konto, das nicht gesperrt
/// ist - also auf Entwickler-PCs ohne Installation und für Eltern-Konten auf Kind-PCs.
/// Fenster lässt sich jederzeit schließen, kein Watchdog, Antworten gehen in ein
/// Wegwerf-Journal, nichts wird freigeschaltet. Die Liste in settings.json kann das
/// Kind nicht ändern, also auch den Entwicklermodus nicht für sich einschalten.
/// "--kindmodus" schaltet für ein nicht gesperrtes Konto das echte Verhalten ein
/// (Watchdog sperrt das eigene Konto, Fortschritt zählt) - nur zum Testen.
/// </summary>
public static class TestMode
{
    public const string ChildModeArgument = "--kindmodus";

    public static readonly string[] OnlyKinds = [JournalKind.Vocabulary, JournalKind.Grammar, JournalKind.VerbForms];

    public static bool IsRequested(string[] args) => args.Any(arg => arg is "--test" or "-test");

    public static bool IsChildModeRequested(string[] args) =>
        args.Any(arg => arg == ChildModeArgument || arg == ChildModeArgument[1..]);

    public static bool IsAllowed(InstallSettings settings, SecurityIdentifier user) => settings.FindUser(user) is null;

    public static bool UsesDeveloperMode(InstallSettings settings, SecurityIdentifier user, string[] args) =>
        IsAllowed(settings, user) && !IsChildModeRequested(args);

    /// <summary>
    /// Ist das Konto laut Installation gesperrt? Ohne Installation (Entwickler-PC) nein.
    /// Unlesbare settings.json gilt als gesperrt - im Zweifel lieber sperren.
    /// </summary>
    public static bool IsBlockedAccount(SecurityIdentifier user)
    {
        try
        {
            return InstallSettings.Exists && !IsAllowed(InstallSettings.Load(), user);
        }
        catch
        {
            return true;
        }
    }

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
