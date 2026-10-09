using System.Windows;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.UI;

public partial class TrophyWindow : Window
{
    private readonly int _required;

    public TrophyWindow(TrainerConfig config)
    {
        InitializeComponent();

        _required = config.RequiredCorrectAnswersFor(System.Environment.MachineName, System.Environment.UserName);
        GoalText.Text = $"Du hast {_required} richtige Antworten geschafft.";
        UnlockedText.Text = $"{config.BlockedAppsText} sind jetzt freigeschaltet! 🚀";
        AppsList.ItemsSource = BlockedAppView.From(config.BlockedApps);

        GameSounds.PlayTrophy();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        AppBlocker.StartSteam(_required);

        Application.Current.Shutdown();
    }
}
