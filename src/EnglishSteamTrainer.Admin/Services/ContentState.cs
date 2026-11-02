using EnglishSteamTrainer.Core;
using Microsoft.JSInterop;

namespace EnglishSteamTrainer.Admin.Services;

public sealed record PendingFile(string Path, string Text, int Added, int Removed);

/// <summary>
/// Die geladenen Inhalte aus content/ (Stand eines Commits auf main) und die Änderungen
/// daran. Alle Seiten arbeiten auf demselben <see cref="Set"/>; gespeichert wird alles
/// zusammen als ein Commit.
/// </summary>
public sealed class ContentState(GitHubApi github, AdminSettings settings, IJSRuntime js)
{
    public const string ContentFolder = "content/";

    private Dictionary<string, string> _original = [];
    private Dictionary<string, HashSet<string>> _originalWords = [];

    public ContentSet Set { get; private set; } = new();
    public bool IsLoaded { get; private set; }
    public string? HeadSha { get; private set; }
    public string? User { get; private set; }
    public DateTime? LoadedAt { get; private set; }
    public string? LastCommitUrl { get; private set; }

    /// <summary>Fehler beim automatischen Verbinden beim Start (gespeichertes Token).</summary>
    public string? ConnectError { get; set; }

    public bool Connecting { get; set; }

    /// <summary>Ohne Anmeldung: Stand von content/ zum Zeitpunkt der Veröffentlichung, Speichern gesperrt.</summary>
    public bool IsDemo { get; private set; }

    /// <summary>Probleme beim Lesen (z. B. Zeilen, die nicht gelesen werden und beim Speichern wegfielen).</summary>
    public List<ContentProblem> LoadProblems { get; } = [];

    public GitHubApi GitHub => github;

    public event Action? Changed;

    public async Task ConnectAsync()
    {
        github.Repository = settings.Repository.Trim();
        github.Token = settings.Token.Trim();
        User = await github.GetUserLoginAsync();
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        var head = await github.GetBranchHeadAsync(AdminSettings.ContentBranch);
        var tree = await github.GetTreeAsync(await github.GetCommitTreeAsync(head));

        var downloads = ContentFiles.All
            .Select(path => (Path: path, Entry: tree.FirstOrDefault(entry => entry.Type == "blob" && entry.Path == ContentFolder + path)))
            .Where(file => file.Entry is not null)
            .Select(async file => (file.Path, Text: Normalize(await github.GetBlobTextAsync(file.Entry!.Sha))));

        var files = (await Task.WhenAll(downloads)).ToDictionary(file => file.Path, file => file.Text);

        IsDemo = false;
        HeadSha = head;
        _original = files;
        LoadedAt = DateTime.Now;
        LastCommitUrl = null;
        Reparse();
        IsLoaded = true;
        NotifyChanged();
    }

    public async Task LoadDemoAsync(string baseUri)
    {
        using var http = new HttpClient { BaseAddress = new Uri(baseUri) };
        var files = new Dictionary<string, string>();

        foreach (var path in ContentFiles.All)
        {
            using var response = await http.GetAsync("demo-content/" + path);

            if (response.IsSuccessStatusCode)
                files[path] = Normalize(await response.Content.ReadAsStringAsync());
        }

        IsDemo = true;
        User = null;
        HeadSha = null;
        _original = files;
        LoadedAt = DateTime.Now;
        LastCommitUrl = null;
        Reparse();
        IsLoaded = true;
        NotifyChanged();
    }

    /// <summary>Alle ungespeicherten Änderungen verwerfen (ohne neu zu laden).</summary>
    public void Discard()
    {
        Reparse();
        NotifyChanged();
    }

    public List<PendingFile> PendingFiles()
    {
        if (!IsLoaded)
            return [];

        return Set.FormatFiles()
            .Where(file => Normalize(_original.GetValueOrDefault(file.Key)) != file.Value)
            .Select(file =>
            {
                var (added, removed) = DiffStat(_original.GetValueOrDefault(file.Key) ?? "", file.Value);
                return new PendingFile(file.Key, file.Value, added, removed);
            })
            .OrderBy(file => file.Path)
            .ToList();
    }

    public List<ContentProblem> Problems() => IsLoaded ? ContentCheck.Check(Set) : [];

    public async Task SaveAsync(string message)
    {
        if (IsDemo)
            throw new InvalidOperationException("Demo-Modus: Speichern ist erst nach der Anmeldung mit Token möglich.");

        var pending = PendingFiles();

        if (pending.Count == 0 || HeadSha is null)
            return;

        var errors = Problems().Where(problem => problem.Severity == ProblemSeverity.Error).ToList();

        if (errors.Count > 0)
            throw new InvalidOperationException($"Erst die {errors.Count} Fehler beheben, dann speichern.");

        var sha = await github.CommitAsync(
            AdminSettings.ContentBranch,
            HeadSha,
            pending.ToDictionary(file => ContentFolder + file.Path, file => file.Text),
            message);

        HeadSha = sha;
        LastCommitUrl = $"https://github.com/{github.Repository}/commit/{sha}";

        foreach (var file in pending)
            _original[file.Path] = file.Text;

        NotifyChanged();
    }

    /// <summary>Stand das Wort schon in der geladenen Datei? Nur dann braucht eine Umbenennung eine Id.</summary>
    public bool IsSavedWord(LearningLanguage language, string word) =>
        _originalWords.TryGetValue(language.Code, out var words) && words.Contains(word.Trim());

    public void NotifyChanged()
    {
        Changed?.Invoke();
        _ = js.InvokeVoidAsync("karteikarten.setUnsaved", PendingFiles().Count > 0);
    }

    public static string Normalize(string? text) => (text ?? "").ReplaceLineEndings("\n");

    private void Reparse()
    {
        LoadProblems.Clear();
        Set = ContentSet.Parse(path => _original.GetValueOrDefault(path), LoadProblems);
        _originalWords = Languages.All.ToDictionary(
            language => language.Code,
            language => Set[language].Vocabulary.Select(card => card.Word).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    // Grobe Zeilenbilanz für die Anzeige vor dem Speichern (Reihenfolge egal).
    private static (int Added, int Removed) DiffStat(string before, string after)
    {
        var remaining = Normalize(before).Split('\n').GroupBy(line => line).ToDictionary(group => group.Key, group => group.Count());
        var added = 0;

        foreach (var line in Normalize(after).Split('\n'))
        {
            if (remaining.TryGetValue(line, out var count) && count > 0)
                remaining[line] = count - 1;
            else
                added++;
        }

        return (added, remaining.Values.Sum());
    }
}
