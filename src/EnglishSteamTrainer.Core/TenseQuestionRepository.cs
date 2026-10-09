namespace EnglishSteamTrainer.Core;

/// <summary>
/// Liest grammar.csv: Subject;Verb;Correct;Distractors;Topic
/// (genau 3 falsche Antworten, mit | getrennt).
/// </summary>
public static class TenseQuestionRepository
{
    public const int DistractorCount = 3;

    public static List<TenseQuestion> Parse(string csv)
    {
        var questions = new List<TenseQuestion>();

        foreach (var line in VocabularyRepository.CsvLines(csv))
        {
            var parts = line.Split(';');

            if (parts.Length < 4)
                continue;

            var correct = parts[2].Trim();
            var distractors = VocabularyRepository.SplitAlternatives(parts, 3)
                .Where(x => x != correct)
                .Distinct()
                .ToList();

            // Die Oberfläche hat genau 4 Antwort-Knöpfe.
            if (correct.Length == 0 || distractors.Count != DistractorCount)
                continue;

            questions.Add(new TenseQuestion(
                parts[0].Trim(),
                parts[1].Trim(),
                correct,
                distractors,
                parts.Length >= 5 && parts[4].Trim().Length > 0 ? parts[4].Trim() : "Grammatik"));
        }

        return questions;
    }
}
