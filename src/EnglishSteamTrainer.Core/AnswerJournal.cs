using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace EnglishSteamTrainer.Core;

public static class JournalKind
{
    public const string Vocabulary = "vocab";
    public const string Grammar = "grammar";
    public const string Import = "import";
}

public sealed record JournalEntry(
    int Sequence,
    DateTime Time,
    string Language,
    string Kind,
    string Item,
    string Prompt,
    string Given,
    AnswerResult? Result,
    int Xp)
{
    public bool CountsAsCorrect => Result is AnswerResult.Correct or AnswerResult.Typo;
}

public sealed record DaySummary(DateOnly Date, int Correct, int Typos, int Wrong, int Xp);

public sealed record MistakeSummary(string Language, string Item, int Wrong, int Correct);

/// <summary>
/// Lernjournal: jede beantwortete Aufgabe wird als eigene Zeile angehängt, inklusive der
/// dafür vergebenen XP. XP, Level, Serien und Tagesfortschritt werden ausschließlich
/// daraus berechnet - es gibt keinen Punktestand, den man einfach überschreiben könnte.
/// Jede Zeile ist mit der vorherigen verkettet signiert: wird eine Zeile von Hand
/// geändert oder gelöscht, gilt ab dort nichts mehr (und die Oberfläche zeigt das an).
/// </summary>
public sealed class AnswerJournal
{
    private const string Header =
        "# Lernjournal English Steam Trainer - Nr|Zeit|Sprache|Art|Aufgabe|Frage|Antwort|Ergebnis|XP|Signatur";

    // Wie in DailyUnlock: kein echtes Geheimnis, schützt aber gegen Ändern von Hand.
    private static readonly byte[] SigningKey =
        Encoding.UTF8.GetBytes("EnglishSteamTrainer|answer-journal|9c41f0d27e8b4a15");

    private readonly SecurityIdentifier _user;
    private readonly string _file;
    private readonly List<JournalEntry> _entries = [];
    private string _lastSignature = "";
    private bool _needsRewrite;

    private AnswerJournal(SecurityIdentifier user, string file)
    {
        _user = user;
        _file = file;
    }

    public IReadOnlyList<JournalEntry> Entries => _entries;

    /// <summary>Beschreibung der ersten ungültigen Zeile, sonst null.</summary>
    public string? IntegrityProblem { get; private set; }

    public string FilePath => _file;

    public int TotalXp => _entries.Sum(entry => entry.Xp);

    public int CurrentStreak
    {
        get
        {
            var streak = 0;

            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Result is null)
                    continue;

                if (!_entries[i].CountsAsCorrect)
                    break;

                streak++;
            }

            return streak;
        }
    }

    public int BestStreak
    {
        get
        {
            int best = 0, current = 0;

            foreach (var entry in _entries.Where(entry => entry.Result is not null))
            {
                current = entry.CountsAsCorrect ? current + 1 : 0;
                best = Math.Max(best, current);
            }

            return best;
        }
    }

    public static AnswerJournal Load(SecurityIdentifier user)
    {
        return Load(user, Path.Combine(DailyUnlock.UnlockFolder, user.Value + ".journal.txt"));
    }

    public static AnswerJournal Load(SecurityIdentifier user, string file)
    {
        var journal = new AnswerJournal(user, file);

        if (!File.Exists(file))
            return journal;

        var lineNumber = 0;

        foreach (var line in File.ReadAllLines(file))
        {
            lineNumber++;

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (!journal.TryAccept(line))
            {
                journal.IntegrityProblem =
                    $"Zeile {lineNumber} wurde verändert oder ist beschädigt. " +
                    $"Gezählt werden nur die {journal._entries.Count} Einträge davor.";
                journal._needsRewrite = true;
                break;
            }
        }

        return journal;
    }

    public int CorrectOn(DateOnly date)
    {
        return _entries.Count(entry => entry.CountsAsCorrect && DateOnly.FromDateTime(entry.Time) == date);
    }

    public JournalEntry RecordAnswer(
        LearningLanguage language,
        string kind,
        string item,
        string prompt,
        string given,
        AnswerResult result)
    {
        var streakAfter = result == AnswerResult.Wrong ? 0 : CurrentStreak + 1;

        return Append(language.Code, kind, item, prompt, given, result, XpRules.ForAnswer(result, streakAfter));
    }

    /// <summary>Übernahme des Punktestands aus der alten progress.json - als eigener, sichtbarer Eintrag.</summary>
    public JournalEntry RecordImport(string description, int xp)
    {
        return Append("", JournalKind.Import, description, "", "", null, xp);
    }

    public List<DaySummary> GetDaySummaries()
    {
        return _entries
            .GroupBy(entry => DateOnly.FromDateTime(entry.Time))
            .OrderByDescending(group => group.Key)
            .Select(group => new DaySummary(
                group.Key,
                group.Count(entry => entry.Result == AnswerResult.Correct),
                group.Count(entry => entry.Result == AnswerResult.Typo),
                group.Count(entry => entry.Result == AnswerResult.Wrong),
                group.Sum(entry => entry.Xp)))
            .ToList();
    }

    public List<MistakeSummary> GetMistakes(int count)
    {
        return _entries
            .Where(entry => entry.Result is not null)
            .GroupBy(entry => (entry.Language, entry.Item))
            .Select(group => new MistakeSummary(
                group.Key.Language,
                group.Key.Item,
                group.Count(entry => entry.Result == AnswerResult.Wrong),
                group.Count(entry => entry.CountsAsCorrect)))
            .Where(summary => summary.Wrong > 0)
            .OrderByDescending(summary => summary.Wrong - summary.Correct)
            .ThenByDescending(summary => summary.Wrong)
            .Take(count)
            .ToList();
    }

    /// <summary>Aufgaben, deren letzter Versuch falsch war - die kommen beim nächsten Mal zuerst.</summary>
    public HashSet<string> GetItemsToRepeat(LearningLanguage language, string kind)
    {
        return _entries
            .Where(entry => entry.Language == language.Code && entry.Kind == kind && entry.Result is not null)
            .GroupBy(entry => entry.Item)
            .Where(group => group.Last().Result == AnswerResult.Wrong)
            .Select(group => group.Key)
            .ToHashSet();
    }

    private JournalEntry Append(
        string language,
        string kind,
        string item,
        string prompt,
        string given,
        AnswerResult? result,
        int xp)
    {
        var entry = new JournalEntry(
            _entries.Count + 1,
            DateTime.Now,
            language,
            kind,
            Clean(item),
            Clean(prompt),
            Clean(given),
            result,
            xp);

        var payload = Format(entry);
        var signature = Sign(_lastSignature, payload);

        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);

        if (_needsRewrite || !File.Exists(_file))
            RewriteFile();

        File.AppendAllText(_file, payload + "|" + signature + Environment.NewLine);

        _entries.Add(entry);
        _lastSignature = signature;
        return entry;
    }

    // Nach einer Manipulation: verdächtige Datei zur Kontrolle aufheben und nur die
    // gültigen Einträge neu schreiben, damit neue Antworten wieder gezählt werden.
    private void RewriteFile()
    {
        if (_needsRewrite && File.Exists(_file))
            File.Copy(_file, $"{_file}.manipuliert-{DateTime.Now:yyyyMMdd-HHmmss}.txt", overwrite: true);

        var lines = new List<string> { Header };
        var previous = "";

        foreach (var entry in _entries)
        {
            var payload = Format(entry);
            previous = Sign(previous, payload);
            lines.Add(payload + "|" + previous);
        }

        var tempFile = _file + ".tmp";
        File.WriteAllLines(tempFile, lines);
        File.Move(tempFile, _file, overwrite: true);
        _needsRewrite = false;
    }

    private bool TryAccept(string line)
    {
        var separator = line.LastIndexOf('|');

        if (separator < 0)
            return false;

        var payload = line[..separator];
        var signature = line[(separator + 1)..];
        var expected = Sign(_lastSignature, payload);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(signature)))
            return false;

        var parts = payload.Split('|');

        if (parts.Length != 9
            || !int.TryParse(parts[0], out var sequence)
            || sequence != _entries.Count + 1
            || !DateTime.TryParseExact(parts[1], "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            || !int.TryParse(parts[8], out var xp))
            return false;

        AnswerResult? result = parts[7] switch
        {
            "richtig" => AnswerResult.Correct,
            "tippfehler" => AnswerResult.Typo,
            "falsch" => AnswerResult.Wrong,
            _ => null
        };

        _entries.Add(new JournalEntry(sequence, time, parts[2], parts[3], parts[4], parts[5], parts[6], result, xp));
        _lastSignature = signature;
        return true;
    }

    private static string Format(JournalEntry entry)
    {
        var result = entry.Result switch
        {
            AnswerResult.Correct => "richtig",
            AnswerResult.Typo => "tippfehler",
            AnswerResult.Wrong => "falsch",
            _ => "uebernahme"
        };

        return string.Join('|',
            entry.Sequence.ToString(CultureInfo.InvariantCulture),
            entry.Time.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            entry.Language,
            entry.Kind,
            entry.Item,
            entry.Prompt,
            entry.Given,
            result,
            entry.Xp.ToString(CultureInfo.InvariantCulture));
    }

    private string Sign(string previousSignature, string payload)
    {
        var data = Encoding.UTF8.GetBytes($"{_user.Value}\n{previousSignature}\n{payload}");
        return Convert.ToHexString(HMACSHA256.HashData(SigningKey, data));
    }

    private static string Clean(string value)
    {
        var cleaned = value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ').Trim();
        return cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }
}
