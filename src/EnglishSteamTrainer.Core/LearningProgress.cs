namespace EnglishSteamTrainer.Core;

public sealed class LearningProgress
{
    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    public int CorrectToday { get; set; }
    public int WrongToday { get; set; }
    public int Xp { get; set; }
    public int BestStreak { get; set; }
    public bool SteamUnlockedToday { get; set; }

    public int Level => Math.Max(1, (Xp / 100) + 1);
    public string Rank => Level switch
    {
        <= 1 => "Starter",
        2 => "Wort-Entdecker",
        3 => "Vokabel-Held",
        4 => "Englisch-Profi",
        _ => "Steam-Meister"
    };
}
