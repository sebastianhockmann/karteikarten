using System.Security.Principal;

namespace EnglishSteamTrainer.Core;

public enum LanguageSource
{
    Central,
    Local,
    Default
}

public sealed record AssignedLanguage(LearningLanguage Language, LanguageSource Source)
{
    public string SourceText => Source switch
    {
        LanguageSource.Central => "zentral festgelegt",
        LanguageSource.Local => "bei der Installation festgelegt",
        _ => "Standard"
    };
}

public static class LanguageAssignment
{
    /// <summary>
    /// 1. content/config.json im Repo (pro PC + Konto oder für alle PCs),
    /// 2. settings.json auf dem PC (Install.ps1),
    /// 3. sonst null - der Aufrufer entscheidet über den Standard.
    /// </summary>
    public static AssignedLanguage? Resolve(
        TrainerConfig config,
        InstallSettings settings,
        SecurityIdentifier user,
        string computer,
        string account)
    {
        var central = config.FindLanguage(computer, account);

        if (central is not null)
            return new AssignedLanguage(central, LanguageSource.Central);

        var local = settings.FindUser(user);

        return local is null ? null : new AssignedLanguage(local.LearningLanguage, LanguageSource.Local);
    }
}
