using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public class ConfigTests
{
    [Fact]
    public void Executables_without_extension_are_completed()
    {
        var config = TrainerConfig.TryParse("""{ "blockedApps": [ { "name": "Firefox", "executables": [ "firefox" ] } ] }""");

        Assert.NotNull(config);
        Assert.Equal(["firefox.exe"], config.BlockedApps[0].Executables);
        Assert.Equal(15, config.RequiredCorrectAnswers);
    }

    [Theory]
    [InlineData("""{ "blockedApps": [] }""")]
    [InlineData("""{ "blockedApps": [ { "name": "Steam", "executables": [] } ] }""")]
    [InlineData("""{ "requiredCorrectAnswers": 0, "blockedApps": [ { "name": "Steam", "executables": [ "steam.exe" ] } ] }""")]
    [InlineData("""{ "blockedApps": """)]
    [InlineData("<html>404</html>")]
    public void Broken_config_is_rejected_instead_of_unlocking_everything(string json)
    {
        Assert.Null(TrainerConfig.TryParse(json));
    }

    [Fact]
    public void Blocked_apps_text_is_readable()
    {
        Assert.Equal("Steam, Chrome, Edge und Discord", TrainerConfig.Default.BlockedAppsText);
    }

    [Fact]
    public void Install_settings_read_users_and_languages()
    {
        var settings = InstallSettings.Parse("""
            { "users": [ { "account": "tiago", "language": "en" }, { "account": "lena", "language": "es" } ] }
            """);

        Assert.Equal(InstallSettings.DefaultContentBaseUrl, settings.ContentBaseUrl);
        Assert.Equal(Languages.Spanish, settings.Users[1].LearningLanguage);
    }

    private const string UsersConfig = """
        {
          "blockedApps": [ { "name": "Steam", "executables": [ "steam.exe" ] } ],
          "users": [
            { "computer": "*", "account": "lena", "language": "en" },
            { "computer": "LAPTOP-LENA", "account": "Lena", "language": "es" },
            { "account": "tiago", "language": "en" }
          ]
        }
        """;

    [Theory]
    [InlineData("laptop-lena", "lena", "es")]
    [InlineData("PC-WOHNZIMMER", "lena", "en")]
    [InlineData("PC-WOHNZIMMER", "TIAGO", "en")]
    [InlineData("PC-WOHNZIMMER", "papa", null)]
    public void Central_language_prefers_exact_computer_over_wildcard(string computer, string account, string? expected)
    {
        var config = TrainerConfig.TryParse(UsersConfig)!;

        Assert.Equal(expected, config.FindLanguage(computer, account)?.Code);
    }

    [Fact]
    public void Required_answers_per_user_fall_back_to_general_value()
    {
        var config = TrainerConfig.TryParse("""
            {
              "requiredCorrectAnswers": 15,
              "blockedApps": [ { "name": "Steam", "executables": [ "steam.exe" ] } ],
              "users": [
                { "computer": "*", "account": "leand", "language": "es", "requiredCorrectAnswers": 30 },
                { "computer": "PC-OMA", "account": "leand", "requiredCorrectAnswers": 10 },
                { "account": "tiago", "language": "en" }
              ]
            }
            """)!;

        Assert.Equal(30, config.RequiredCorrectAnswersFor("PC-ZUHAUSE", "Leand"));
        Assert.Equal(10, config.RequiredCorrectAnswersFor("PC-OMA", "leand"));
        Assert.Equal("es", config.FindLanguage("PC-OMA", "leand")?.Code);
        Assert.Equal(15, config.RequiredCorrectAnswersFor("PC-ZUHAUSE", "tiago"));
        Assert.Equal(15, config.RequiredCorrectAnswersFor("PC-ZUHAUSE", "papa"));
    }

    [Theory]
    [InlineData("""{ "account": "lena", "language": "spanish" }""")]
    [InlineData("""{ "account": "", "language": "es" }""")]
    [InlineData("""{ "account": "lena", "requiredCorrectAnswers": 0 }""")]
    public void Typo_in_user_assignment_rejects_config(string user)
    {
        var json = """{ "blockedApps": [ { "name": "Steam", "executables": [ "steam.exe" ] } ], "users": [ USER ] }"""
            .Replace("USER", user);

        Assert.Null(TrainerConfig.TryParse(json));
    }

    [Fact]
    public void Central_assignment_wins_over_local_installation()
    {
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var config = TrainerConfig.TryParse(UsersConfig)!;
        var settings = InstallSettings.Parse($$"""{ "users": [ { "account": "{{Environment.UserName}}", "language": "en" } ] }""");

        var local = LanguageAssignment.Resolve(config, settings, sid, "ANY-PC", Environment.UserName);
        Assert.Equal(LanguageSource.Local, local?.Source);

        config.Users.Add(new UserSettings("ANY-PC", Environment.UserName, "es"));
        var central = LanguageAssignment.Resolve(config, settings, sid, "ANY-PC", Environment.UserName);
        Assert.Equal(LanguageSource.Central, central?.Source);
        Assert.Equal(Languages.Spanish, central?.Language);
    }

    [Fact]
    public void Grammar_lines_need_exactly_three_distractors()
    {
        var questions = TenseQuestionRepository.Parse("""
            Subject;Verb;Correct;Distractors;Topic
            yo;hablar;hablo;hablas|habla|hablamos;Presente
            yo;comer;como;comes|come;Presente
            """);

        Assert.Single(questions);
        Assert.Equal("Presente", questions[0].Hint);
    }
}
