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
}
