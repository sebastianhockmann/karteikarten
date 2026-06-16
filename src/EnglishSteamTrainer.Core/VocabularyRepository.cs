using System;
using System.Collections.Generic;
using System.IO;

namespace EnglishSteamTrainer.Core;

public static class VocabularyRepository
{
    public static string CsvFilePath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "vocabulary.csv");

    public static List<VocabularyCard> LoadDefaultCards()
    {
        var csvFile = CsvFilePath;

        if (!File.Exists(csvFile))
        {
            return LoadFallbackCards();
        }

        var cards = new List<VocabularyCard>();
        var lines = File.ReadAllLines(csvFile);

        foreach (var line in lines[1..])
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(';');

            if (parts.Length < 3)
                continue;

            var alternatives = new List<string>();

            if (parts.Length >= 4)
            {
                alternatives = parts[3]
                    .Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .ToList();
            }

            cards.Add(new VocabularyCard(
        parts[0].Trim(),
        parts[1].Trim(),
        parts[2].Trim(),
        alternatives));
        }

        return cards.Count > 0 ? cards : LoadFallbackCards();
    }

    private static List<VocabularyCard> LoadFallbackCards()
    {
        return new List<VocabularyCard>
    {
        new VocabularyCard(
            "apple",
            "Apfel",
            "Obst rot oder grün",
            new List<string>()),

        new VocabularyCard(
            "night",
            "Nacht",
            "Dunkle Tageszeit",
            new List<string>()),

        new VocabularyCard(
            "happy",
            "glücklich",
            "Gegenteil von traurig",
            new List<string> { "fröhlich", "froh" })
    };
    }
}