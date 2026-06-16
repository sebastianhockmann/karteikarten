using System;
using System.IO;
using System.Text.Json;

namespace EnglishSteamTrainer.Core;

public static class ProgressStore
{
    private static readonly string AppFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EnglishSteamTrainer");

    private static readonly string ProgressFile =
        Path.Combine(AppFolder, "progress.json");

    public static LearningProgress Load()
    {
        Directory.CreateDirectory(AppFolder);

        if (!File.Exists(ProgressFile))
        {
            var progress = new LearningProgress();
            Save(progress);
            return progress;
        }

        var json = File.ReadAllText(ProgressFile);

        return JsonSerializer.Deserialize<LearningProgress>(json)
               ?? new LearningProgress();
    }

    public static void Save(LearningProgress progress)
    {
        Directory.CreateDirectory(AppFolder);

        var json = JsonSerializer.Serialize(
            progress,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(ProgressFile, json);
    }
}