using System;
using System.IO;
using System.Media;

namespace EnglishSteamTrainer.UI;

internal static class GameSounds
{
    private static readonly string SoundsFolder = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Sounds");

    public static void PlayCorrect() => Play("correct.wav");
    public static void PlayWrong() => Play("wrong.wav");
    public static void PlayTrophy() => Play("trophy.wav");
    public static void PlayLevelUp() => Play("levelup.wav");

    private static void Play(string fileName)
    {
        try
        {
            var path = Path.Combine(SoundsFolder, fileName);

            if (!File.Exists(path))
                return;

            new SoundPlayer(path).Play();
        }
        catch
        {
            // Kein Ton verfügbar - Lernfortschritt darf davon nicht abhängen.
        }
    }
}
