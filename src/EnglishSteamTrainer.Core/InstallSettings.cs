using System.Security.Principal;
using System.Text.Json;

namespace EnglishSteamTrainer.Core;

/// <summary>
/// Einstellungen dieser Installation (settings.json im Programmordner). Liegt unter
/// C:\ProgramData\EnglishSteamTrainer und ist dort nur für Admins beschreibbar -
/// das Kind kann sich also weder entsperren noch die Sprache wechseln.
/// Gepflegt von scripts\Install.ps1.
/// </summary>
public sealed class InstallSettings
{
    public const string DefaultRepository = "sebastianhockmann/karteikarten";

    public const string DefaultContentBaseUrl =
        "https://raw.githubusercontent.com/" + DefaultRepository + "/main/content/";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string Repository { get; set; } = DefaultRepository;
    public string ContentBaseUrl { get; set; } = DefaultContentBaseUrl;
    public List<UserProfile> Users { get; set; } = [];

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    // Vorgänger-Format: ein Kontoname pro Zeile, Sprache immer Englisch.
    private static string LegacyBlockedUsersFile => Path.Combine(AppContext.BaseDirectory, "blocked-users.txt");

    public static bool Exists => File.Exists(FilePath) || File.Exists(LegacyBlockedUsersFile);

    public static InstallSettings Load()
    {
        if (File.Exists(FilePath))
            return Parse(File.ReadAllText(FilePath));

        var settings = new InstallSettings();

        if (File.Exists(LegacyBlockedUsersFile))
        {
            settings.Users = File.ReadAllLines(LegacyBlockedUsersFile)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .Select(name => new UserProfile { Account = name, Language = Languages.English.Code })
                .ToList();
        }

        return settings;
    }

    public static InstallSettings Parse(string json)
    {
        var settings = JsonSerializer.Deserialize<InstallSettings>(json, JsonOptions)
                       ?? new InstallSettings();

        if (string.IsNullOrWhiteSpace(settings.Repository))
            settings.Repository = DefaultRepository;

        if (string.IsNullOrWhiteSpace(settings.ContentBaseUrl))
            settings.ContentBaseUrl = DefaultContentBaseUrl;

        if (!settings.ContentBaseUrl.EndsWith('/'))
            settings.ContentBaseUrl += "/";

        settings.Users ??= [];
        return settings;
    }

    public UserProfile? FindUser(SecurityIdentifier user)
    {
        return Users.FirstOrDefault(profile => profile.TryGetSid() == user);
    }
}

public sealed class UserProfile
{
    public string Account { get; set; } = "";
    public string Language { get; set; } = Languages.English.Code;

    public LearningLanguage LearningLanguage => Languages.Find(Language) ?? Languages.English;

    public SecurityIdentifier? TryGetSid()
    {
        try
        {
            return (SecurityIdentifier)new NTAccount(Account).Translate(typeof(SecurityIdentifier));
        }
        catch
        {
            return null;
        }
    }
}
