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

    public static TheoryData<string> AllFiles => new(ContentFiles.All);

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Every_content_file_is_valid(string relativePath)
    {
        Assert.True(ContentFiles.IsValid(relativePath, Read(relativePath)), $"{relativePath} ist ungültig.");
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void No_line_is_silently_dropped(string relativePath)
    {
        if (relativePath.EndsWith(".json"))
            return;

        var text = Read(relativePath);
        var dataLines = text.Split('\n').Skip(1).Count(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'));
        var parsed = ParseKeys(relativePath, text).Count;

        Assert.Equal(dataLines, parsed);
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void No_duplicate_entries(string relativePath)
    {
        if (relativePath.EndsWith(".json"))
            return;

        var text = Read(relativePath);
        var keys = ParseKeys(relativePath, text);

        var duplicates = keys.GroupBy(key => key).Where(group => group.Count() > 1).Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    private static List<string> ParseKeys(string relativePath, string text)
    {
        if (relativePath.EndsWith("vocabulary.csv"))
            return VocabularyRepository.Parse(text).Select(card => card.Key.ToLowerInvariant()).ToList();

        if (relativePath.EndsWith("verbs.csv"))
            return VerbFormsRepository.Parse(text).Select(verb => verb.Key.ToLowerInvariant()).ToList();

        return TenseQuestionRepository.Parse(text).Select(question => question.Key).ToList();
    }

    [Fact]
    public void Config_blocks_at_least_steam()
    {
        var config = TrainerConfig.TryParse(Read(ContentFiles.Config));

        Assert.NotNull(config);
        Assert.Contains(config.BlockedApps, app => app.Executables.Contains("steam.exe"));
    }

    [Fact]
    public void Content_check_finds_no_errors()
    {
        var problems = new List<ContentProblem>();
        var set = ContentSet.Parse(Read, problems);
        problems.AddRange(ContentCheck.Check(set));

        Assert.Empty(problems.Where(problem => problem.Severity == ProblemSeverity.Error).Select(problem => problem.ToString()));
    }

    // Die Pflege-Oberfläche liest und schreibt die Dateien - dabei darf nichts verloren gehen
    // und sich nichts ändern, was niemand angefasst hat.
    [Fact]
    public void Writing_the_content_back_changes_nothing()
    {
        var set = ContentSet.Parse(Read, []);

        foreach (var (path, text) in set.FormatFiles())
        {
            Assert.Equal(Read(path).ReplaceLineEndings(), text.ReplaceLineEndings());
        }
    }
}
