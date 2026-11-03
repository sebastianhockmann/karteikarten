namespace EnglishSteamTrainer.Core;

/// <summary>
/// Motiv für die Lern-App (wie die Google-Doodles): Maskottchen, Deko und Farben.
/// Wechselt jede Woche, an Feiertagen gibt es ein passendes Sondermotiv.
/// </summary>
/// <param name="HeaderColor">Dunkle Farbe der Kopfzeile (weiße Schrift darauf).</param>
/// <param name="BackgroundColor">Helle Fensterfarbe.</param>
public sealed record Motif(
    string Id,
    string Name,
    string Mascot,
    string[] Decorations,
    string HeaderColor,
    string BackgroundColor,
    string Greeting);

/// <remarks>
/// Die App zeichnet Emojis farbig mit Emoji.Wpf (Emoji-Schrift von Windows). Neue Emojis
/// trotzdem einmal ansehen, z. B. über die Motiv-Vorschau im Entwicklermodus.
/// </remarks>
public static class Motifs
{
    // Erster Montag, ab dem die Wochen gezählt werden. Gleiche Woche = gleiches Motiv auf allen PCs.
    private static readonly DateOnly RotationStart = new(2026, 1, 5);

    /// <summary>Wechseln Woche für Woche in dieser Reihenfolge.</summary>
    public static readonly IReadOnlyList<Motif> Weekly =
    [
        new("weltraum", "Weltraum", "🚀", ["🌍", "⭐", "🌙", "☄️"], "#1A237E", "#EEF0FF", "Ab ins All!"),
        new("ozean", "Ozean", "🐙", ["🐠", "🐚", "🌊", "🐳"], "#01579B", "#E6F6FF", "Tauch ab in die Wörter!"),
        new("dinos", "Dinosaurier", "🦖", ["🦕", "🌋", "🥚", "🌿"], "#4E342E", "#F8F1E7", "Brüll dich durch die Aufgaben!"),
        new("ritter", "Ritter & Drachen", "🐉", ["🏰", "🛡️", "⚔️", "👑"], "#4A148C", "#F5EEFB", "Erobere die Burg!"),
        new("dschungel", "Dschungel", "🐒", ["🌴", "🦜", "🍌", "🌺"], "#1B5E20", "#EFFAE8", "Schwing dich von Wort zu Wort!"),
        new("roboter", "Roboter", "🤖", ["⚙️", "🔋", "💡", "🛰️"], "#37474F", "#EEF3F5", "Beep boop – los geht's!"),
        new("piraten", "Piraten", "🦜", ["⚓", "💰", "🗺️", "⛵"], "#3E2723", "#FFF5E1", "Ahoi! Finde den Schatz!"),
        new("fussball", "Fußball", "⚽", ["🏆", "🥅", "👟", "📣"], "#2E7D32", "#ECF8EC", "Schieß ein Tor nach dem anderen!"),
        new("arktis", "Arktis", "🐧", ["❄️", "🧊", "⛄", "🐋"], "#0D47A1", "#EEF7FF", "Cool bleiben!"),
        new("pixel", "Videospiel", "👾", ["🕹️", "🎮", "💎", "🍄"], "#311B92", "#F1EEFF", "Level up!"),
        new("safari", "Safari", "🦒", ["🦁", "🐘", "🦓", "🌅"], "#BF360C", "#FFF3E8", "Auf Entdeckungstour!"),
        new("westen", "Wilder Westen", "🤠", ["🌵", "🐎", "⭐", "🏜️"], "#6D4C41", "#FFF4E6", "Yeehaw!"),
        new("rennen", "Rennstrecke", "🏎️", ["🏁", "🏆", "⛽", "🚦"], "#212121", "#F3F3F3", "Vollgas!"),
        new("musik", "Musik", "🎸", ["🎵", "🥁", "🎤", "🎹"], "#880E4F", "#FFEFF6", "Rock die Vokabeln!"),
        new("erfinder", "Erfinder", "🧪", ["🔬", "💡", "🧲", "⚗️"], "#004D40", "#E8F7F5", "Experimentier mit Wörtern!"),
        new("zirkus", "Zirkus", "🦁", ["🎪", "🎈", "🤹", "🎠"], "#B71C1C", "#FFF0F0", "Manege frei!")
    ];

    public static readonly Motif NewYear = new("neujahr", "Neujahr", "🎆", ["🎉", "🥳", "✨", "🍀"], "#1A1A2E", "#FFF9E6", "Frohes neues Jahr!");
    public static readonly Motif Carnival = new("fasching", "Fasching", "🎭", ["🎉", "🤡", "🎊", "🎈"], "#AD1457", "#FFF0F7", "Helau und Alaaf!");
    public static readonly Motif Easter = new("ostern", "Ostern", "🐰", ["🥚", "🌷", "🐣", "🌼"], "#6A1B9A", "#FBF6FF", "Frohe Ostern!");
    public static readonly Motif Halloween = new("halloween", "Halloween", "🎃", ["👻", "🦇", "🕷️", "🍬"], "#212121", "#FFF1E0", "Süßes oder Saures!");
    public static readonly Motif Advent = new("advent", "Advent", "⛄", ["🕯️", "❄️", "🍪", "🌟"], "#1B5E20", "#F2FAF2", "Schöne Adventszeit!");
    public static readonly Motif Christmas = new("weihnachten", "Weihnachten", "🎅", ["🎄", "🎁", "⭐", "🔔"], "#B71C1C", "#FFF5F5", "Frohe Weihnachten!");

    public static readonly IReadOnlyList<Motif> Holidays = [NewYear, Carnival, Easter, Halloween, Advent, Christmas];

    public static IEnumerable<Motif> All => Holidays.Concat(Weekly);

    public static Motif? Find(string? id) =>
        All.FirstOrDefault(motif => string.Equals(motif.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Feiertagsmotiv, sonst das Motiv der Woche (Montag bis Sonntag).</summary>
    public static Motif For(DateOnly date) => HolidayOn(date) ?? WeeklyFor(date);

    public static Motif WeeklyFor(DateOnly date)
    {
        var weeks = (int)Math.Floor((date.DayNumber - RotationStart.DayNumber) / 7.0);
        var count = Weekly.Count;

        return Weekly[((weeks % count) + count) % count];
    }

    public static Motif? HolidayOn(DateOnly date)
    {
        var easter = EasterSunday(date.Year);

        return date switch
        {
            { Month: 12, Day: >= 28 } or { Month: 1, Day: <= 4 } => NewYear,
            { Month: 12, Day: >= 18 } => Christmas,
            { Month: 12 } => Advent,
            { Month: 10, Day: >= 25 } or { Month: 11, Day: 1 } => Halloween,
            // Weiberfastnacht bis Aschermittwoch.
            _ when date >= easter.AddDays(-52) && date <= easter.AddDays(-46) => Carnival,
            // Palmsonntag bis Ostermontag.
            _ when date >= easter.AddDays(-7) && date <= easter.AddDays(1) => Easter,
            _ => null
        };
    }

    /// <summary>Ostersonntag nach der gregorianischen Osterformel (Meeus/Jones/Butcher).</summary>
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;

        return new DateOnly(year, month, day);
    }
}
