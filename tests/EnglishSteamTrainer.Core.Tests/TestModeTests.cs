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
    public void Arguments_are_parsed()
    {
        Assert.True(TestMode.IsRequested(["--test"]));
        Assert.False(TestMode.IsRequested(["--language", "es"]));

        var options = TestMode.Parse(["--test", "--language", "es", "--only", "Verbs"]);

        Assert.Equal(Languages.Spanish, options.Language);
        Assert.Equal(JournalKind.VerbForms, options.Only);
    }

    [Fact]
    public void Unknown_only_value_means_all_kinds()
    {
        var options = TestMode.Parse(["--test", "--only", "alles"]);

        Assert.Null(options.Only);
        Assert.Null(options.Language);
    }
}
