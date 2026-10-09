namespace EnglishSteamTrainer.Core;

public sealed record LearningLanguage(
    string Code,
    string Name,
    string Flag,
    string[] Articles
);

public static class Languages
{
    // Artikel werden beim Vergleich ignoriert: "der Hund", "el perro" und "the dog"
    // zählen genauso wie "Hund", "perro" und "dog".
    public static readonly string[] GermanArticles = ["der", "die", "das", "ein", "eine"];

    public static readonly LearningLanguage English =
        new("en", "Englisch", "🇬🇧", ["the", "a", "an", "to"]);

    public static readonly LearningLanguage Spanish =
        new("es", "Spanisch", "🇪🇸", ["el", "la", "los", "las", "un", "una"]);

    public static readonly IReadOnlyList<LearningLanguage> All = [English, Spanish];

    public static LearningLanguage? Find(string? code)
    {
        return All.FirstOrDefault(language =>
            string.Equals(language.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
