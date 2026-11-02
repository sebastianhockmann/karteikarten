using System.Text.RegularExpressions;
using EnglishSteamTrainer.Core;

namespace EnglishSteamTrainer.Core.Tests;

public class MotifTests
{
    [Theory]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]
    [InlineData(2030, 4, 21)]
    public void Easter_sunday_is_computed(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), Motifs.EasterSunday(year));
    }

    [Theory]
    [InlineData("2026-10-31", "halloween")]
    [InlineData("2026-11-01", "halloween")]
    [InlineData("2026-12-06", "advent")]
    [InlineData("2026-12-24", "weihnachten")]
    [InlineData("2026-12-31", "neujahr")]
    [InlineData("2027-01-02", "neujahr")]
    [InlineData("2027-02-08", "fasching")]
    [InlineData("2027-03-29", "ostern")]
    public void Holidays_get_their_own_motif(string date, string expected)
    {
        Assert.Equal(expected, Motifs.For(DateOnly.Parse(date)).Id);
    }

    [Fact]
    public void Motif_stays_the_same_from_monday_to_sunday_and_changes_each_week()
    {
        var monday = new DateOnly(2026, 11, 2);

        Assert.Null(Motifs.HolidayOn(monday));

        for (var day = 1; day < 7; day++)
            Assert.Equal(Motifs.For(monday), Motifs.For(monday.AddDays(day)));

        for (var week = 0; week < 104; week++)
        {
            var date = monday.AddDays(7 * week);
            Assert.NotEqual(Motifs.WeeklyFor(date), Motifs.WeeklyFor(date.AddDays(7)));
        }

        // Auch vor dem Start der Zählung gibt es ein Motiv.
        Assert.Contains(Motifs.WeeklyFor(new DateOnly(2025, 6, 1)), Motifs.Weekly);
    }

    [Fact]
    public void Motifs_are_complete()
    {
        var all = Motifs.All.ToList();
        var color = new Regex("^#[0-9A-F]{6}$");

        Assert.Equal(all.Count, all.Select(motif => motif.Id).Distinct().Count());
        Assert.All(all, motif =>
        {
            Assert.Matches(color, motif.HeaderColor);
            Assert.Matches(color, motif.BackgroundColor);
            Assert.NotEmpty(motif.Decorations);
            Assert.False(string.IsNullOrWhiteSpace(motif.Mascot));
            Assert.Same(motif, Motifs.Find(motif.Id.ToUpperInvariant()));
        });
    }
}
