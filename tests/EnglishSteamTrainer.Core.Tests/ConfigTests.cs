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
