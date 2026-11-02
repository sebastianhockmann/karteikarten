using System.Security.Principal;

namespace EnglishSteamTrainer.Core;

/// <param name="Language">"--language es": Sprache unabhängig von der Zuordnung.</param>
/// <param name="Only">"--only verbs|grammar|vocab": nur diese Art von Aufgaben.</param>
/// <param name="Account">"--als tiago": wie dieses Kind aus config.json lernen (Sprache, Lernplan, Anzahl).</param>
public sealed record TestOptions(LearningLanguage? Language, string? Only, string? Account = null);

/// <summary>Ein Kind aus config.json, als das der Entwicklermodus lernen kann.</summary>
/// <param name="Computer">PC aus dem Eintrag, damit PC-spezifische Werte (z. B. Anzahl) greifen.</param>
public sealed record SimulatedLearner(string Account, string Computer, LearningLanguage Language)
{
    public string Label => $"{Account} {Language.Flag} {Language.Name}";
}

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
        var account = ValueAfter(args, "--als")?.Trim();

        return new TestOptions(
            language,
            only is not null && OnlyKinds.Contains(only) ? only : null,
            string.IsNullOrEmpty(account) ? null : account);
    }

    /// <summary>Kinder aus config.json (users), je Konto einmal.</summary>
    public static List<SimulatedLearner> Learners(TrainerConfig config, string fallbackComputer) =>
        config.Users
            .GroupBy(user => user.Account.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var computer = group
                    .Select(user => user.Computer?.Trim())
                    .FirstOrDefault(c => !string.IsNullOrEmpty(c) && c != "*") ?? fallbackComputer;

                return new SimulatedLearner(
                    group.Key,
                    computer,
                    config.FindLanguage(computer, group.Key) ?? Languages.English);
            })
            .OrderBy(learner => learner.Account, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static SimulatedLearner? FindLearner(TrainerConfig config, string? account, string fallbackComputer) =>
        account is null
            ? null
            : Learners(config, fallbackComputer).FirstOrDefault(learner =>
                string.Equals(learner.Account, account, StringComparison.OrdinalIgnoreCase));

    internal static string? ValueAfter(string[] args, string name)
    {
        var index = Array.FindIndex(args, arg => arg == name || arg == name[1..]);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
