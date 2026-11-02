using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public class TestModeTests
{
    private static readonly System.Security.Principal.SecurityIdentifier CurrentUser =
        System.Security.Principal.WindowsIdentity.GetCurrent().User!;

    [Fact]
    public void Locked_account_cannot_use_test_mode()
    {
        var settings = InstallSettings.Parse($$"""{ "users": [ { "account": "{{Environment.UserName}}", "language": "en" } ] }""");

        Assert.False(TestMode.IsAllowed(settings, CurrentUser));
    }

    [Fact]
    public void Unlocked_account_can_use_test_mode()
    {
        var settings = InstallSettings.Parse("""{ "users": [ { "account": "kein-solches-konto", "language": "en" } ] }""");

        Assert.True(TestMode.IsAllowed(settings, CurrentUser));
        Assert.True(TestMode.IsAllowed(new InstallSettings(), CurrentUser));
    }

    [Fact]
    public void Unlocked_account_gets_developer_mode_unless_child_mode_is_requested()
    {
        var notInstalled = new InstallSettings();
        var locked = InstallSettings.Parse($$"""{ "users": [ { "account": "{{Environment.UserName}}", "language": "en" } ] }""");

        Assert.True(TestMode.UsesDeveloperMode(notInstalled, CurrentUser, []));
        Assert.True(TestMode.UsesDeveloperMode(notInstalled, CurrentUser, ["--test"]));
        Assert.False(TestMode.UsesDeveloperMode(notInstalled, CurrentUser, ["--kindmodus"]));
        Assert.False(TestMode.UsesDeveloperMode(locked, CurrentUser, []));
        Assert.False(TestMode.UsesDeveloperMode(locked, CurrentUser, ["--test"]));
    }

    [Fact]
    public void Arguments_are_parsed()
    {
        Assert.True(TestMode.IsRequested(["--test"]));
        Assert.False(TestMode.IsRequested(["--language", "es"]));

        var options = TestMode.Parse(["--test", "--language", "es", "--only", "Verbs"]);

        Assert.Equal(Languages.Spanish, options.Language);
        Assert.Equal(JournalKind.VerbForms, options.Only);
    }

    [Fact]
    public void Learners_come_from_config_with_their_computer_and_language()
    {
        var config = TrainerConfig.TryParse("""
            {
              "blockedApps": [ { "name": "Steam", "executables": [ "steam.exe" ] } ],
              "users": [
                { "computer": "DESKTOP-NO7VIKD", "account": "tiago", "language": "en", "requiredCorrectAnswers": 15 },
                { "computer": "Groot", "account": "leand", "language": "es", "requiredCorrectAnswers": 30 },
                { "computer": "*", "account": "Leand", "requiredCorrectAnswers": 20 }
              ]
            }
            """)!;

        var learners = TestMode.Learners(config, "DEV-PC");

        Assert.Equal(["leand", "tiago"], learners.Select(learner => learner.Account));
        Assert.Equal(new SimulatedLearner("leand", "Groot", Languages.Spanish), learners[0]);
        Assert.Equal(30, config.RequiredCorrectAnswersFor(learners[0].Computer, learners[0].Account));
        Assert.Equal(new SimulatedLearner("tiago", "DESKTOP-NO7VIKD", Languages.English), TestMode.FindLearner(config, "TIAGO", "DEV-PC"));
        Assert.Null(TestMode.FindLearner(config, "unbekannt", "DEV-PC"));
        Assert.Equal("tiago", TestMode.Parse(["--als", "tiago"]).Account);
    }

    [Fact]
    public void Unknown_only_value_means_all_kinds()
    {
        var options = TestMode.Parse(["--test", "--only", "alles"]);

        Assert.Null(options.Only);
        Assert.Null(options.Language);
    }
}
