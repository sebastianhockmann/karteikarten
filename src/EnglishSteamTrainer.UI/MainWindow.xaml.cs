using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.UI;

public partial class MainWindow : Window
{
    private static readonly TimeSpan AdvanceDelay = TimeSpan.FromMilliseconds(900);

    private readonly LearningLanguage _language;
    private readonly AssignedLanguage _assignedLanguage;
    private readonly AnswerJournal _journal;
    private readonly LoadedContent<TrainerConfig> _config;
    private readonly LoadedContent<List<VocabularyCard>> _vocabCards;
    private readonly LoadedContent<List<TenseQuestion>> _tenseQuestions;
    private readonly LoadedContent<List<VerbForms>> _verbForms;
    private readonly TestOptions? _test;
    private readonly Random _random = new();

    private readonly Queue<QuizItem> _quizQueue = new();
    private readonly DispatcherTimer _advanceTimer;

    private QuizItem? _currentItem;
    private bool _currentItemSolved;
    private DateOnly _correctTodayDate;
    private int _correctToday;

    /// <param name="test">Gesetzt = Testmodus (siehe <see cref="TestMode"/>).</param>
    public MainWindow(AssignedLanguage assignedLanguage, AnswerJournal journal, TestOptions? test = null)
    {
        InitializeComponent();

        _test = test;

        if (_test is null)
            StartWatchdog();

        _assignedLanguage = assignedLanguage;
        _language = assignedLanguage.Language;
        _journal = journal;
        _config = ContentStore.LoadConfig();
        _vocabCards = ContentStore.LoadVocabulary(_language);
        _tenseQuestions = ContentStore.LoadGrammar(_language);
        _verbForms = ContentStore.LoadVerbForms(_language);

        _advanceTimer = new DispatcherTimer { Interval = AdvanceDelay };
        _advanceTimer.Tick += (_, _) =>
        {
            _advanceTimer.Stop();
            ShowItem();
        };

        ApplyLanguageAndConfig();
        RefreshCorrectToday();
        BuildQuizQueue();

        UpdateUi();

        if (_correctToday >= RequiredCorrectAnswers)
        {
            ShowAlreadyDoneState();
        }
        else if (_quizQueue.Count == 0)
        {
            ShowNoContentState();
        }
        else
        {
            ShowItem();
        }
    }

    private int RequiredCorrectAnswers =>
        _config.Value.RequiredCorrectAnswersFor(Environment.MachineName, Environment.UserName);
    private string BlockedAppsText => _config.Value.BlockedAppsText;

    private void ApplyLanguageAndConfig()
    {
        Title = $"{_language.Name}-Trainer";
        TitleText.Text = $"🎮 {_language.Name}-Trainer {_language.Flag}";
        SubtitleText.Text =
            $"Löse {RequiredCorrectAnswers} Aufgaben. Danach werden diese Programme für heute freigeschaltet:";

        if (_test is not null)
        {
            Title += " (Testmodus)";
            TitleText.Text = "🧪 TESTMODUS · " + TitleText.Text;
            SubtitleText.Text =
                "Fenster lässt sich jederzeit schließen. Antworten werden nicht gespeichert, nichts wird freigeschaltet." +
                (_test.Only is null ? "" : $" Nur Aufgaben: {_test.Only}.");
        }

        var apps = BlockedAppView.From(_config.Value.BlockedApps);
        HeaderAppsList.ItemsSource = apps;
        RewardAppsList.ItemsSource = apps;
        StatusAppsList.ItemsSource = apps;

        var lastSync = ContentStore.GetLastSync();
        var contentText = _vocabCards.Source switch
        {
            ContentSource.OnlineCache when lastSync is not null => $"online, Stand {lastSync:dd.MM.yyyy HH:mm}",
            ContentSource.OnlineCache => "online",
            ContentSource.Bundled => "mitgeliefert (noch nicht online abgeglichen)",
            _ => "keine gefunden"
        };

        VersionText.Text =
            $"Version {AppVersion.Current} · {Environment.MachineName}\\{Environment.UserName}: {_language.Name} ({_assignedLanguage.SourceText}) · {_vocabCards.Value.Count} Vokabeln, " +
            $"{_tenseQuestions.Value.Count} Grammatik-Fragen{VerbFormsCountText} · Inhalte: {contentText}";
    }

    private string VerbFormsCountText =>
        _language.HasVerbForms ? $", {_verbForms.Value.Count} Verben" : "";

    private TextBox[] VerbBoxes => [VerbBox1, VerbBox2, VerbBox3];

    private void BuildQuizQueue()
    {
        _quizQueue.Clear();

        // Was beim letzten Versuch falsch war, kommt zuerst dran.
        var repeatVocab = _journal.GetItemsToRepeat(_language, JournalKind.Vocabulary);
        var repeatGrammar = _journal.GetItemsToRepeat(_language, JournalKind.Grammar);
        var repeatVerbs = _journal.GetItemsToRepeat(_language, JournalKind.VerbForms);

        var items = new List<QuizItem>();

        foreach (var card in IsIncluded(JournalKind.Vocabulary) ? _vocabCards.Value : [])
        {
            items.Add(new VocabQuizItem(card, _random.Next(2) == 0));
        }

        var tensePool = (IsIncluded(JournalKind.Grammar) ? _tenseQuestions.Value : [])
            .OrderByDescending(question => repeatGrammar.Contains(question.Key))
            .ThenBy(_ => _random.Next())
            .Take(_config.Value.GrammarQuestionsPerSession);

        foreach (var question in tensePool)
        {
            var options = new List<string> { question.CorrectForm };
            options.AddRange(question.Distractors);
            options = options.OrderBy(_ => _random.Next()).ToList();

            items.Add(new TenseQuizItem(question, options));
        }

        var verbPool = (IsIncluded(JournalKind.VerbForms) ? _verbForms.Value : [])
            .OrderByDescending(verb => repeatVerbs.Contains(verb.Key))
            .ThenBy(_ => _random.Next())
            .Take(_config.Value.VerbFormQuestionsPerSession);

        foreach (var verb in verbPool)
        {
            items.Add(new VerbFormsQuizItem(verb, _random.Next(VerbForms.FormNames.Length)));
        }

        var ordered = items
            .OrderBy(_ => _random.Next())
            .OrderByDescending(item => item switch
            {
                VocabQuizItem vocab => repeatVocab.Contains(vocab.Card.Word),
                TenseQuizItem tense => repeatGrammar.Contains(tense.Question.Key),
                VerbFormsQuizItem verb => repeatVerbs.Contains(verb.Verb.Key),
                _ => false
            });

        foreach (var item in ordered)
        {
            _quizQueue.Enqueue(item);
        }
    }

    private bool IsIncluded(string kind) => _test?.Only is null || _test.Only == kind;

    private void ShowAlreadyDoneState()
    {
        VocabPanel.Visibility = Visibility.Collapsed;
        TensePanel.Visibility = Visibility.Collapsed;
        VerbPanel.Visibility = Visibility.Collapsed;

        FeedbackBox.Background = (Brush)FindResource("FeedbackCorrectBgBrush");
        FeedbackText.Text = "🏆 Du hast dein heutiges Ziel schon geschafft!";
        FeedbackText.Foreground = (Brush)FindResource("PrimaryGreenDarkBrush");
        FeedbackText.Visibility = Visibility.Visible;
    }

    private void ShowNoContentState()
    {
        VocabPanel.Visibility = Visibility.Collapsed;
        TensePanel.Visibility = Visibility.Collapsed;
        VerbPanel.Visibility = Visibility.Collapsed;

        FeedbackBox.Background = (Brush)FindResource("FeedbackWrongBgBrush");
        FeedbackText.Text = $"😕 Für {_language.Name} wurden keine Aufgaben gefunden. Bitte sag deinen Eltern Bescheid.";
        FeedbackText.Foreground = (Brush)FindResource("DangerRedDarkBrush");
        FeedbackText.Visibility = Visibility.Visible;
    }

    private void ShowItem()
    {
        if (_quizQueue.Count == 0)
        {
            BuildQuizQueue();
        }

        if (_quizQueue.Count == 0)
        {
            ShowNoContentState();
            return;
        }

        _currentItem = _quizQueue.Dequeue();
        _currentItemSolved = false;

        FeedbackText.Text = "";
        FeedbackText.Visibility = Visibility.Hidden;
        FeedbackBox.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));

        switch (_currentItem)
        {
            case VocabQuizItem vocabItem:
                ShowVocabItem(vocabItem);
                break;

            case TenseQuizItem tenseItem:
                ShowTenseItem(tenseItem);
                break;

            case VerbFormsQuizItem verbItem:
                ShowVerbItem(verbItem);
                break;
        }
    }

    private void ShowVocabItem(VocabQuizItem item)
    {
        VocabPanel.Visibility = Visibility.Visible;
        TensePanel.Visibility = Visibility.Collapsed;
        VerbPanel.Visibility = Visibility.Collapsed;

        WordText.Text = item.AskWordToGerman
            ? item.Card.Word
            : item.Card.German;

        AnswerBox.Text = "";
        AnswerBox.IsEnabled = true;

        VocabHeaderText.Text = item.AskWordToGerman
            ? "Was heißt dieses Wort auf Deutsch?"
            : $"Was heißt dieses Wort auf {_language.Name}?";

        HintText.Text = item.AskWordToGerman
            ? $"{_language.Flag} → 🇩🇪 Übersetze ins Deutsche"
            : $"🇩🇪 → {_language.Flag} Übersetze ins {_language.Name}e";

        AnswerBox.Focus();
    }

    private void ShowTenseItem(TenseQuizItem item)
    {
        VocabPanel.Visibility = Visibility.Collapsed;
        TensePanel.Visibility = Visibility.Visible;
        VerbPanel.Visibility = Visibility.Collapsed;

        GrammarHeaderText.Text = $"Welche Form ist richtig? ({item.Question.Hint})";
        TenseQuestionText.Text = item.Question.Key;

        var buttons = new[] { TenseOption1, TenseOption2, TenseOption3, TenseOption4 };

        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].Content = item.ShuffledOptions[i];
            buttons[i].Tag = item.ShuffledOptions[i];
            buttons[i].IsEnabled = true;
            buttons[i].ClearValue(BackgroundProperty);
            buttons[i].ClearValue(BorderBrushProperty);
        }
    }

    // Eine Form ist vorgegeben, die anderen beiden werden eingetippt.
    private void ShowVerbItem(VerbFormsQuizItem item)
    {
        VocabPanel.Visibility = Visibility.Collapsed;
        TensePanel.Visibility = Visibility.Collapsed;
        VerbPanel.Visibility = Visibility.Visible;

        VerbHeaderText.Text = $"Ergänze die fehlenden Formen ({VerbForms.FormNames[item.GivenForm]} ist vorgegeben)";
        VerbGermanText.Text = item.Verb.German;

        var boxes = VerbBoxes;

        for (var i = 0; i < boxes.Length; i++)
        {
            var given = i == item.GivenForm;

            boxes[i].Text = given ? item.Verb.Display(i) : "";
            boxes[i].IsReadOnly = given;
            boxes[i].Focusable = !given;
            boxes[i].Background = given ? new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF1)) : Brushes.White;
            boxes[i].ClearValue(BorderBrushProperty);
        }

        boxes.First(box => !box.IsReadOnly).Focus();
    }

    // Normalerweise läuft der Watchdog bereits als SYSTEM (scripts\Install.ps1).
    // Das hier ist nur der Ersatz für Entwicklung oder falls er nicht läuft;
    // er sperrt ausschließlich das aktuelle Konto.
    private static void StartWatchdog()
    {
        try
        {
            var watchdogPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "EnglishSteamTrainer.Watchdog.exe");

            if (!System.IO.File.Exists(watchdogPath))
                return;

            // Installiert, aber dieses Konto (z. B. Eltern) ist gar nicht gesperrt.
            if (InstallSettings.Exists
                && InstallSettings.Load().FindUser(DailyUnlock.CurrentUser) is null)
                return;

            if (System.Diagnostics.Process.GetProcessesByName("EnglishSteamTrainer.Watchdog").Length > 0)
                return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = watchdogPath,
                UseShellExecute = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            });
        }
        catch
        {
        }
    }

    private void CheckAnswer_Click(object sender, RoutedEventArgs e)
    {
        CheckAnswer();
    }

    private void AnswerBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            CheckAnswer();
    }

    // Enter springt ins nächste leere Feld, im letzten Feld wird geprüft.
    private void VerbBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        var next = VerbBoxes
            .SkipWhile(box => box != sender)
            .Skip(1)
            .FirstOrDefault(box => !box.IsReadOnly && string.IsNullOrWhiteSpace(box.Text));

        if (next is not null && !_currentItemSolved)
            next.Focus();
        else
            CheckAnswer();
    }

    private void CheckAnswer()
    {
        if (_currentItem is VerbFormsQuizItem verbItem)
        {
            CheckVerbAnswer(verbItem);
            return;
        }

        if (_currentItem is not VocabQuizItem vocabItem)
            return;

        if (_currentItemSolved)
        {
            _advanceTimer.Stop();
            ShowItem();
            return;
        }

        var given = AnswerBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(given))
        {
            FeedbackText.Text = "✍️ Bitte gib zuerst eine Antwort ein.";
            FeedbackText.Foreground = Brushes.DarkOrange;
            FeedbackText.Visibility = Visibility.Visible;
            return;
        }

        var validAnswers = AnswerChecker.ValidAnswers(vocabItem.Card, vocabItem.AskWordToGerman);
        var check = AnswerChecker.Check(given, validAnswers, _language);
        var direction = vocabItem.AskWordToGerman ? $"{_language.Code}→de" : $"de→{_language.Code}";

        var entry = Record(
            JournalKind.Vocabulary,
            vocabItem.Card.Word,
            $"{WordText.Text} ({direction})",
            given,
            check.Result);

        if (check.Result == AnswerResult.Wrong)
        {
            HandleWrongAnswer(validAnswers);
            return;
        }

        var note = check.Result == AnswerResult.Typo
            ? $"Fast richtig! Richtig geschrieben: {check.Expected}"
            : !string.Equals(given, check.Expected, StringComparison.OrdinalIgnoreCase)
                ? $"Richtig! Genau geschrieben: {check.Expected}"
                : "Richtig!";

        HandleCorrectAnswer(entry, note);
    }

    // Die Lösung bleibt danach stehen, bis Enter oder "Nächste Karte" kommt.
    private void CheckVerbAnswer(VerbFormsQuizItem item)
    {
        if (_currentItemSolved)
        {
            _advanceTimer.Stop();
            ShowItem();
            return;
        }

        var boxes = VerbBoxes;
        var asked = Enumerable.Range(0, boxes.Length).Where(i => i != item.GivenForm).ToList();
        var empty = asked.Where(i => string.IsNullOrWhiteSpace(boxes[i].Text)).ToList();

        if (empty.Count > 0)
        {
            FeedbackText.Text = "✍️ Bitte fülle zuerst alle Felder aus.";
            FeedbackText.Foreground = Brushes.DarkOrange;
            FeedbackText.Visibility = Visibility.Visible;
            boxes[empty[0]].Focus();
            return;
        }

        var checks = asked.ToDictionary(
            i => i,
            i => AnswerChecker.Check(boxes[i].Text, AnswerChecker.ValidAnswers(item.Verb, i), _language));
        var result = AnswerChecker.Combine(checks.Values);

        foreach (var (i, check) in checks)
        {
            boxes[i].IsReadOnly = true;
            boxes[i].BorderBrush = check.Result == AnswerResult.Wrong
                ? (Brush)FindResource("DangerRedBrush")
                : (Brush)FindResource("PrimaryGreenBrush");
        }

        var given = string.Join(" / ", asked.Select(i => boxes[i].Text.Trim()));
        var prompt = $"{item.Verb.Display(item.GivenForm)} ({VerbForms.FormNames[item.GivenForm]}) → " +
                     string.Join(", ", asked.Select(i => VerbForms.FormNames[i]));
        var entry = Record(JournalKind.VerbForms, item.Verb.Key, prompt, given, result);
        var allForms = string.Join(" – ", Enumerable.Range(0, boxes.Length).Select(item.Verb.Display));

        if (result == AnswerResult.Wrong)
        {
            _currentItemSolved = true;
            HandleWrongAnswer([allForms]);
            return;
        }

        var note = result == AnswerResult.Typo
            ? $"Fast richtig! Richtig geschrieben: {allForms}"
            : $"Richtig! {allForms}";

        HandleCorrectAnswer(entry, note);
    }

    private void TenseOption_Click(object sender, RoutedEventArgs e)
    {
        if (_currentItem is not TenseQuizItem tenseItem || _currentItemSolved)
            return;

        var clicked = (Button)sender;
        var chosen = (string)clicked.Tag;

        var buttons = new[] { TenseOption1, TenseOption2, TenseOption3, TenseOption4 };

        foreach (var button in buttons)
        {
            button.IsEnabled = false;

            var optionText = (string)button.Tag;

            if (optionText == tenseItem.Question.CorrectForm)
            {
                button.Background = (Brush)FindResource("PrimaryGreenBrush");
                button.BorderBrush = (Brush)FindResource("PrimaryGreenDarkBrush");
            }
            else if (button == clicked)
            {
                button.Background = (Brush)FindResource("DangerRedBrush");
                button.BorderBrush = (Brush)FindResource("DangerRedDarkBrush");
            }
        }

        _currentItemSolved = true;

        var result = chosen == tenseItem.Question.CorrectForm ? AnswerResult.Correct : AnswerResult.Wrong;
        var entry = Record(JournalKind.Grammar, tenseItem.Question.Key, tenseItem.Question.Key, chosen, result);

        if (result == AnswerResult.Correct)
        {
            HandleCorrectAnswer(entry, "Richtig!");
        }
        else
        {
            HandleWrongAnswer(new List<string> { tenseItem.Question.CorrectForm });
        }
    }

    private JournalEntry? Record(string kind, string item, string prompt, string given, AnswerResult result)
    {
        // Vor dem Eintragen, sonst zählt die neue Antwort beim Tageswechsel doppelt.
        RefreshCorrectToday();

        try
        {
            return _journal.RecordAnswer(_language, kind, item, prompt, given, result);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Die Antwort konnte nicht im Lernjournal gespeichert werden:\n{ex.Message}",
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }
    }

    private void HandleCorrectAnswer(JournalEntry? entry, string note)
    {
        _currentItemSolved = true;

        var previousLevel = XpRules.Level(_journal.TotalXp - (entry?.Xp ?? 0));

        _correctToday++;
        SaveCorrectToday();

        ((Storyboard)FindResource("MascotHappyBounce")).Begin(this);

        FeedbackBox.Background = (Brush)FindResource("FeedbackCorrectBgBrush");

        var level = XpRules.Level(_journal.TotalXp);

        if (level > previousLevel)
        {
            GameSounds.PlayLevelUp();
            ShowLevelUpBanner();
        }
        else
        {
            GameSounds.PlayCorrect();
        }

        if (IsUnlocked())
        {
            FeedbackText.Text = $"🏆 Pokal gewonnen! {BlockedAppsText} sind jetzt freigeschaltet!";
            FeedbackText.Foreground = (Brush)FindResource("PrimaryGreenDarkBrush");
            FeedbackText.Visibility = Visibility.Visible;

            UpdateUi();

            RewardText.Text =
                $"🏆 Herzlichen Glückwunsch! Du hast {RequiredCorrectAnswers} richtige Antworten geschafft. {BlockedAppsText} sind jetzt freigeschaltet!";

            var trophyWindow = new TrophyWindow(_config.Value)
            {
                Owner = this
            };

            trophyWindow.ShowDialog();
            return;
        }

        var xp = entry?.Xp ?? 0;
        var streakBonus = entry?.Result is { } result && xp > XpRules.ForAnswer(result, 0);

        FeedbackText.Text = streakBonus
            ? $"✅ {note} 🔥 {_journal.CurrentStreak} in Folge! +{xp} XP"
            : $"✅ {note} +{xp} XP";
        FeedbackText.Foreground = (Brush)FindResource("PrimaryGreenDarkBrush");
        FeedbackText.Visibility = Visibility.Visible;

        AnswerBox.IsEnabled = false;

        UpdateUi();

        _advanceTimer.Start();
    }

    private void HandleWrongAnswer(List<string> validAnswers)
    {
        var correctText = string.Join(" / ", validAnswers);

        FeedbackText.Text =
            $"❌ Leider falsch. Richtige Antwort: {correctText}";

        FeedbackText.Foreground = (Brush)FindResource("DangerRedDarkBrush");
        FeedbackText.Visibility = Visibility.Visible;
        FeedbackBox.Background = (Brush)FindResource("FeedbackWrongBgBrush");

        GameSounds.PlayWrong();
        ((Storyboard)FindResource("MascotWrongShake")).Begin(this);

        if (_currentItem is not null)
        {
            RequeueItemLater(_currentItem);
        }

        UpdateUi();

        if (_currentItem is TenseQuizItem)
        {
            _advanceTimer.Start();
        }
    }

    private void RequeueItemLater(QuizItem item)
    {
        var temp = _quizQueue.ToList();
        _quizQueue.Clear();

        var insertAfter = Math.Min(3, temp.Count);

        for (var i = 0; i < temp.Count; i++)
        {
            _quizQueue.Enqueue(temp[i]);

            if (i == insertAfter - 1)
            {
                _quizQueue.Enqueue(item);
            }
        }

        if (temp.Count == 0)
        {
            _quizQueue.Enqueue(item);
        }
    }

    private void Hint_Click(object sender, RoutedEventArgs e)
    {
        if (_currentItem is not VocabQuizItem vocabItem)
            return;

        HintText.Text = string.IsNullOrWhiteSpace(vocabItem.Card.Hint)
            ? "💡 Für dieses Wort gibt es leider keinen Tipp."
            : "💡 Tipp: " + vocabItem.Card.Hint;
        System.Media.SystemSounds.Question.Play();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _advanceTimer.Stop();
        ShowItem();
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        new HistoryWindow(_journal, _config.Value)
        {
            Owner = this
        }.ShowDialog();

        AnswerBox.Focus();
    }

    private void Steam_Click(object sender, RoutedEventArgs e)
    {
        if (_test is not null)
        {
            Close();
            return;
        }

        AppBlocker.StartSteam(RequiredCorrectAnswers);
        Application.Current.Shutdown();
    }

    private void UpdateUi()
    {
        var solved = Math.Min(RequiredCorrectAnswers, _correctToday);
        var remaining = Math.Max(0, RequiredCorrectAnswers - solved);
        var xp = _journal.TotalXp;
        var level = XpRules.Level(xp);

        LevelText.Text = $"Level {level}: {XpRules.Rank(level, _language)}";
        XpText.Text = $"{xp} XP";
        CurrentStreakText.Text = $"🔥 Serie: {_journal.CurrentStreak}";
        BestStreakText.Text = $"Beste Serie: {_journal.BestStreak}";

        ProgressBar.Maximum = RequiredCorrectAnswers;
        ProgressBar.Value = solved;

        ProgressText.Text =
            remaining == 0
                ? $"🏆 Geschafft! {RequiredCorrectAnswers} von {RequiredCorrectAnswers}"
                : $"{solved} / {RequiredCorrectAnswers} richtige Antworten heute";

        var unlocked = IsUnlocked();
        SteamButton.IsEnabled = unlocked;

        StatusText.Text =
            unlocked
                ? $"🎮 {BlockedAppsText} sind freigeschaltet."
                : $"🔒 {BlockedAppsText} sind gesperrt, bis {RequiredCorrectAnswers} Aufgaben richtig beantwortet wurden.";

        if (!unlocked)
        {
            RewardText.Text =
                $"⭐ Noch {remaining} richtige Antwort(en), dann sind {BlockedAppsText} freigeschaltet.";
        }
    }

    private void ShowLevelUpBanner()
    {
        var level = XpRules.Level(_journal.TotalXp);

        LevelUpBannerText.Text = $"🎉 Level up! Level {level}: {XpRules.Rank(level, _language)}";
        LevelUpBanner.Visibility = Visibility.Visible;

        var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            LevelUpBanner.Visibility = Visibility.Collapsed;
        };
        hideTimer.Start();
    }

    // Die signierte Datei ist die Wahrheit - genau die prüft auch der Watchdog.
    private bool IsUnlocked()
    {
        if (_test is not null)
            return _correctToday >= RequiredCorrectAnswers;

        return DailyUnlock.IsUnlockedToday(DailyUnlock.CurrentUser, RequiredCorrectAnswers);
    }

    // Beim Tageswechsel (App bleibt über Mitternacht offen) wieder bei 0 beginnen.
    // Am Tag des Umstiegs auf das Lernjournal zählt zusätzlich die alte Tagesdatei.
    private void RefreshCorrectToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        if (_correctTodayDate == today)
            return;

        _correctTodayDate = today;

        if (_test is not null)
        {
            _correctToday = _journal.CorrectOn(today);
            return;
        }

        _correctToday = Math.Max(
            _journal.CorrectOn(today),
            DailyUnlock.GetCorrectToday(DailyUnlock.CurrentUser));
    }

    private void SaveCorrectToday()
    {
        if (_test is not null)
            return;

        try
        {
            DailyUnlock.SetCorrectToday(_correctToday);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Der Fortschritt konnte nicht gespeichert werden:\n{ex.Message}",
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_test is null && !IsUnlocked())
        {
            e.Cancel = true;

            MessageBox.Show(
                $"🔒 {BlockedAppsText} sind noch gesperrt.\n\n" +
                $"Du musst erst {RequiredCorrectAnswers} Aufgaben richtig beantworten.\n\n" +
                $"Aktueller Fortschritt: {_correctToday} von {RequiredCorrectAnswers}",
                "Noch nicht fertig 🙂",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
