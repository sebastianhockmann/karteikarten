namespace EnglishSteamTrainer.Core;

public static class QuizOrder
{
    /// <summary>
    /// Mischt die Aufgaben zum Thema der Woche nach vorn: Jeder Platz wird mit
    /// <paramref name="focusSharePercent"/> % Wahrscheinlichkeit aus <paramref name="focus"/>
    /// gefüllt, bis eine der Listen leer ist. Die Reihenfolge innerhalb beider Listen bleibt.
    /// </summary>
    public static List<T> MixFocus<T>(IReadOnlyList<T> focus, IReadOnlyList<T> others, int focusSharePercent, Random random)
    {
        var result = new List<T>(focus.Count + others.Count);
        int f = 0, o = 0;

        while (f < focus.Count && o < others.Count)
        {
            result.Add(random.Next(100) < focusSharePercent ? focus[f++] : others[o++]);
        }

        result.AddRange(focus.Skip(f));
        result.AddRange(others.Skip(o));

        return result;
    }

    /// <summary>
    /// Verteilt <paramref name="extra"/> zufällig auf die ersten <paramref name="window"/> Plätze
    /// von <paramref name="main"/> (z. B. Grammatik zwischen die fälligen Vokabeln). Die
    /// Reihenfolge innerhalb beider Listen bleibt.
    /// </summary>
    public static List<T> Interleave<T>(IReadOnlyList<T> main, IReadOnlyList<T> extra, int window, Random random)
    {
        var span = Math.Min(main.Count, Math.Max(window, 0)) + extra.Count;
        var extraSlots = Enumerable.Range(0, span).OrderBy(_ => random.Next()).Take(extra.Count).ToHashSet();
        var result = new List<T>(main.Count + extra.Count);
        int m = 0, e = 0;

        for (var slot = 0; slot < span; slot++)
        {
            result.Add(extraSlots.Contains(slot) ? extra[e++] : main[m++]);
        }

        result.AddRange(main.Skip(m));

        return result;
    }
}
