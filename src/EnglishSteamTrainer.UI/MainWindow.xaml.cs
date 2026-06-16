using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Input;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.UI;

public partial class MainWindow : Window
{
    private const int RequiredCorrectAnswers = 15;

    private readonly List<VocabularyCard> _cards = VocabularyRepository.LoadDefaultCards();
    private readonly Random _random = new();

    private readonly Queue<VocabularyCard> _cardQueue = new();

    private VocabularyCard _currentCard;
    private LearningProgress _progress;
    private bool _currentCardSolved;
    private bool _askEnglishToGerman = true;
    private int _sessionCorrectCount = 0;

    public MainWindow()
    {
        InitializeComponent();
        StartWatchdog();

        _progress = ProgressStore.Load();

        _progress.CorrectToday = 0;
        _progress.WrongToday = 0;
        _progress.SteamUnlockedToday = false;
        _sessionCorrectCount = 0;

        ProgressStore.Save(_progress);

        BuildCardQueue();

        _currentCard = _cardQueue.Peek();

        UpdateUi();
        ShowCard();
    }

    private void BuildCardQueue()
    {
        _cardQueue.Clear();

        foreach (var card in _cards.OrderBy(_ => _random.Next()))
        {
            _cardQueue.Enqueue(card);
        }
    }

    private void ShowCard()
    {
        if (_cardQueue.Count == 0)
        {
            BuildCardQueue();
        }

        _currentCard = _cardQueue.Dequeue();
        _currentCardSolved = false;
        _askEnglishToGerman = _random.Next(2) == 0;

        WordText.Text = _askEnglishToGerman
            ? _currentCard.English
            : _currentCard.German;

        AnswerBox.Text = "";
        AnswerBox.IsEnabled = true;

        HintText.Text = _askEnglishToGerman
            ? "🇬🇧 → 🇩🇪 Übersetze ins Deutsche"
            : "🇩🇪 → 🇬🇧 Übersetze ins Englische";

        FeedbackText.Text = "";
        FeedbackText.Visibility = Visibility.Hidden;

        AnswerBox.Focus();
    }

    private static void StartWatchdog()
    {
        try
        {
            var watchdogPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "EnglishSteamTrainer.Watchdog.exe");

            if (!System.IO.File.Exists(watchdogPath))
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

    private void CheckAnswer()
    {
        if (_currentCardSolved)
        {
            ShowCard();
            return;
        }

        var answer = Normalize(AnswerBox.Text);

        if (string.IsNullOrWhiteSpace(answer))
        {
            FeedbackText.Text = "✍️ Bitte gib zuerst eine Antwort ein.";
            FeedbackText.Foreground = System.Windows.Media.Brushes.DarkOrange;
            FeedbackText.Visibility = Visibility.Visible;
            return;
        }

        var validAnswers = GetValidAnswers(_currentCard, _askEnglishToGerman);

        var isCorrect =
            validAnswers.Any(x => Normalize(x) == answer)
            || IsFuzzyMatch(answer, validAnswers);

        if (isCorrect)
        {
            HandleCorrectAnswer();
        }
        else
        {
            HandleWrongAnswer(validAnswers);
        }
    }

    private void HandleCorrectAnswer()
    {
        _currentCardSolved = true;

        _sessionCorrectCount++;
        _progress.CorrectToday = _sessionCorrectCount;

        _progress.Xp += 10;

        if (_sessionCorrectCount % 3 == 0)
            _progress.Xp += 5;

        SystemSounds.Asterisk.Play();

        if (_sessionCorrectCount >= RequiredCorrectAnswers)
        {
            _progress.SteamUnlockedToday = true;

            FeedbackText.Text = "🏆 Pokal gewonnen! Steam ist jetzt freigeschaltet!";
            FeedbackText.Foreground = System.Windows.Media.Brushes.ForestGreen;
            FeedbackText.Visibility = Visibility.Visible;

            RewardText.Text =
                "🏆 Herzlichen Glückwunsch! Du hast 15 richtige Antworten geschafft. Steam ist jetzt freigeschaltet!";

            ProgressStore.Save(_progress);
            UpdateUi();

            var trophyWindow = new TrophyWindow
            {
                Owner = this
            };

            trophyWindow.ShowDialog();
            return;
        }

        FeedbackText.Text = "✅ Richtig! 🚀 Nächstes Wort kommt sofort.";
        FeedbackText.Foreground = System.Windows.Media.Brushes.ForestGreen;
        FeedbackText.Visibility = Visibility.Visible;

        ProgressStore.Save(_progress);
        UpdateUi();

        ShowCard();
    }

    private void HandleWrongAnswer(List<string> validAnswers)
    {
        _progress.WrongToday++;

        var correctText = string.Join(" / ", validAnswers);

        FeedbackText.Text =
            $"❌ Leider falsch. Richtige Antwort: {correctText}";

        FeedbackText.Foreground = System.Windows.Media.Brushes.OrangeRed;
        FeedbackText.Visibility = Visibility.Visible;

        SystemSounds.Hand.Play();

        RequeueWrongCardLater(_currentCard);

        ProgressStore.Save(_progress);
        UpdateUi();
    }

    private void RequeueWrongCardLater(VocabularyCard card)
    {
        var temp = _cardQueue.ToList();
        _cardQueue.Clear();

        var insertAfter = Math.Min(3, temp.Count);

        for (var i = 0; i < temp.Count; i++)
        {
            _cardQueue.Enqueue(temp[i]);

            if (i == insertAfter - 1)
            {
                _cardQueue.Enqueue(card);
            }
        }

        if (temp.Count == 0)
        {
            _cardQueue.Enqueue(card);
        }
    }

    private static List<string> GetValidAnswers(
        VocabularyCard card,
        bool askEnglishToGerman)
    {
        if (askEnglishToGerman)
        {
            var answers = new List<string> { card.German };
            answers.AddRange(card.Alternatives);
            return answers;
        }

        return new List<string> { card.English };
    }

    private static bool IsFuzzyMatch(string answer, List<string> validAnswers)
    {
        foreach (var validAnswer in validAnswers)
        {
            var normalizedValid = Normalize(validAnswer);

            if (answer == normalizedValid)
                return true;

            if (LevenshteinDistance(answer, normalizedValid) <= 1)
                return true;
        }

        return false;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var matrix = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++)
            matrix[i, 0] = i;

        for (var j = 0; j <= b.Length; j++)
            matrix[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[a.Length, b.Length];
    }

    private static string Normalize(string value)
    {
        return value.Trim()
            .ToLowerInvariant()
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");
    }

    private void Hint_Click(object sender, RoutedEventArgs e)
    {
        HintText.Text = "💡 Tipp: " + _currentCard.Hint;
        SystemSounds.Question.Play();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        ShowCard();
    }

    private void Steam_Click(object sender, RoutedEventArgs e)
    {
        SteamBlocker.StartSteam();
        Application.Current.Shutdown();
    }

    private void UpdateUi()
    {
        var solved = Math.Min(RequiredCorrectAnswers, _sessionCorrectCount);
        var remaining = Math.Max(0, RequiredCorrectAnswers - solved);

        LevelText.Text = $"Level {_progress.Level}: {_progress.Rank}";
        XpText.Text = $"{_progress.Xp} XP";

        ProgressBar.Maximum = RequiredCorrectAnswers;
        ProgressBar.Value = solved;

        ProgressText.Text =
            remaining == 0
                ? $"🏆 Geschafft! {RequiredCorrectAnswers} von {RequiredCorrectAnswers}"
                : $"{solved} / {RequiredCorrectAnswers} richtige Karten heute";

        SteamButton.IsEnabled = _progress.SteamUnlockedToday;

        StatusText.Text =
            _progress.SteamUnlockedToday
                ? "🎮 Steam ist freigeschaltet."
                : $"🔒 Steam ist gesperrt, bis {RequiredCorrectAnswers} Karten richtig beantwortet wurden.";

        if (!_progress.SteamUnlockedToday)
        {
            RewardText.Text =
                $"⭐ Noch {remaining} richtige Antwort(en) bis zur Freischaltung.";
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_progress.SteamUnlockedToday)
        {
            e.Cancel = true;

            MessageBox.Show(
                $"🔒 Steam ist noch gesperrt.\n\n" +
                $"Du musst erst {RequiredCorrectAnswers} Vokabeln richtig beantworten.\n\n" +
                $"Aktueller Fortschritt: {_sessionCorrectCount} von {RequiredCorrectAnswers}",
                "Noch nicht fertig 🙂",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}