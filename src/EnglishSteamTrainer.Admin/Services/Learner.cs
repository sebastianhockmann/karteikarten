using System.Globalization;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Admin.Services;

/// <param name="Language">Null, wenn die Sprache nicht zentral festgelegt ist (dann gilt die vom PC).</param>
public sealed record Learner(string Account, LearningLanguage? Language)
{
    public string Label => Language is null ? Account : $"{Account} {Language.Flag}";

    /// <summary>Kinder aus config.json (users), je Konto einmal.</summary>
    public static List<Learner> From(TrainerConfig config) =>
        config.Users
            .GroupBy(user => user.Account.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new Learner(group.Key, group.Select(user => Languages.Find(user.Language)).FirstOrDefault(l => l is not null)))
            .OrderBy(learner => learner.Account, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

public static class Dates
{
    private static readonly string[] DayNames = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static string Short(DateOnly date) => date.ToString("dd.MM.", CultureInfo.InvariantCulture);

    public static string Long(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string WithDay(DateOnly date) => $"{DayNames[(int)date.DayOfWeek]} {Short(date)}";

    public static string Range(DateOnly from, DateOnly to) => $"{Short(from)}–{Short(to)}";

    public static int Week(DateOnly date) => ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
}
