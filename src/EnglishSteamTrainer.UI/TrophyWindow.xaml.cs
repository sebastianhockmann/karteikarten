using System.Windows;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.UI;

public partial class TrophyWindow : Window
{
    private readonly TrainerConfig _config;

    public TrophyWindow(TrainerConfig config)
    {
        InitializeComponent();

        _config = config;
        GoalText.Text = $"Du hast {config.RequiredCorrectAnswers} richtige Antworten geschafft.";
        UnlockedText.Text = $"{config.BlockedAppsText} sind jetzt freigeschaltet! 🚀";
        AppsList.ItemsSource = BlockedAppView.From(config.BlockedApps);

        GameSounds.PlayTrophy();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        AppBlocker.StartSteam(_config.RequiredCorrectAnswers);

        Application.Current.Shutdown();
    }
}
