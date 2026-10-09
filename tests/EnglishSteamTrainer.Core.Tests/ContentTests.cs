using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

/// <summary>
/// Prüft die echten Dateien aus content/ - so fällt ein Tippfehler in der CSV vor dem
/// Push auf (die GitHub-Action führt diese Tests ebenfalls aus).
/// </summary>
public class ContentTests
{
    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", relativePath));

    public static TheoryData<string> AllFiles => new(ContentStore.AllFiles);

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Every_content_file_is_valid(string relativePath)
    {
        Assert.True(ContentStore.IsValid(relativePath, Read(relativePath)), $"{relativePath} ist ungültig.");
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void No_line_is_silently_dropped(string relativePath)
    {
        if (relativePath == ContentStore.ConfigFile)
            return;

        var text = Read(relativePath);
        var dataLines = text.Split('\n').Skip(1).Count(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'));
        var parsed = relativePath.EndsWith("vocabulary.csv")
            ? VocabularyRepository.Parse(text).Count
            : TenseQuestionRepository.Parse(text).Count;

        Assert.Equal(dataLines, parsed);
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void No_duplicate_entries(string relativePath)
    {
        if (relativePath == ContentStore.ConfigFile)
            return;

        var text = Read(relativePath);
        var keys = relativePath.EndsWith("vocabulary.csv")
            ? VocabularyRepository.Parse(text).Select(card => card.Word.ToLowerInvariant()).ToList()
            : TenseQuestionRepository.Parse(text).Select(question => question.Key).ToList();

        var duplicates = keys.GroupBy(key => key).Where(group => group.Count() > 1).Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Config_blocks_at_least_steam()
    {
        var config = TrainerConfig.TryParse(Read(ContentStore.ConfigFile));

        Assert.NotNull(config);
        Assert.Contains(config.BlockedApps, app => app.Executables.Contains("steam.exe"));
    }
}
