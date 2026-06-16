using System.Windows;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.UI;

public partial class TrophyWindow : Window
{
    public TrophyWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        SteamBlocker.StartSteam();

        Application.Current.Shutdown();
    }
}