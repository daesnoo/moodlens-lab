using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace TelegramMood;

public sealed partial class EmotionAnalyzer
{
    private static readonly string[] Labels = ["joy", "sadness", "anger", "fear", "surprise", "neutral"];
    private static readonly Dictionary<string, string[]> Training = new()
    {
        ["joy"] = ["я очень счастлив сегодня", "это прекрасная новость", "ура мы победили", "как же здорово", "я так рад тебя видеть", "у меня всё получилось", "настроение отличное", "обожаю такие моменты", "мечта сбылась", "я горжусь результатом"],
        ["sadness"] = ["мне очень грустно", "хочется плакать", "я сильно расстроился", "это печальная новость", "мне одиноко", "ничего не получилось", "так жаль", "я скучаю", "у меня тоска", "я разочарован"],
        ["anger"] = ["я ужасно зол", "это меня бесит", "сколько можно терпеть", "я возмущён", "перестань раздражать", "это несправедливо", "я в ярости", "меня всё достало", "это недопустимо", "хватит врать"],
        ["fear"] = ["мне очень страшно", "я боюсь", "меня тревожит будущее", "вдруг случится плохое", "я сильно волнуюсь", "это опасно", "у меня паника", "я переживаю", "мне не по себе", "боюсь ошибиться"],
        ["surprise"] = ["ничего себе", "вот это да", "я не могу поверить", "какая неожиданность", "вот это поворот", "серьёзно это правда", "я не ожидал", "невероятно", "ого какой сюрприз", "это меня поразило"],
        ["neutral"] = ["сегодня понедельник", "урок начинается в девять", "я отправил файл", "на столе книга", "встреча завтра", "автобус приехал", "нужно выполнить задание", "сообщение получено", "я иду в магазин", "расписание изменилось"]
    };
    private static readonly Dictionary<string, string[]> Signals = new()
    {
        ["joy"] = ["рад", "счастлив", "ура", "здорово", "отличн", "прекрасн", "люблю", "обожаю", "побед", "получилось", "горжусь", "класс"],
        ["sadness"] = ["груст", "печал", "плак", "слёз", "расстро", "одинок", "жаль", "скучаю", "тоск", "разочар", "пусто"],
        ["anger"] = ["зол", "бесит", "возмущ", "раздраж", "несправедлив", "ярост", "ненавиж", "достал", "недопуст", "хватит", "врать"],
        ["fear"] = ["страш", "боюсь", "тревож", "вдруг", "волнуюсь", "опасн", "паник", "пережива", "не по себе", "ошибк"],
        ["surprise"] = ["ого", "неожидан", "удив", "невероят", "сюрприз", "пораз", "не ожидал", "не могу поверить", "вот это"],
        ["neutral"] = []
    };
    private readonly Dictionary<string, Dictionary<string, int>> _counts = [];
    private readonly Dictionary<string, int> _totals = [];
    private readonly HashSet<string> _vocabulary = [];

    public EmotionAnalyzer(IEnumerable<(string Text, string Label)>? trainingExamples = null, bool includeBuiltInTraining = true)
    {
        if (includeBuiltInTraining)
        {
            foreach (var label in Labels)
            {
                foreach (var sentence in Training[label]) AddExample(sentence, label);
            }
        }

        if (trainingExamples is not null)
            foreach (var (text, label) in trainingExamples)
                if (Labels.Contains(label) && !string.IsNullOrWhiteSpace(text)) AddExample(text, label);
    }

    public static EmotionAnalyzer FromCsv(params string[] paths)
    {
        var examples = paths
            .Where(File.Exists)
            .SelectMany(path => File.ReadLines(path).Skip(1))
            .Select(ParseCsvRow)
            .Where(row => row is not null)
            .Select(row => row!.Value);
        return new EmotionAnalyzer(examples);
    }

    private void AddExample(string sentence, string label)
    {
        _counts.TryAdd(label, []);
        foreach (var token in Tokenize(sentence))
        {
            _vocabulary.Add(token);
            _counts[label][token] = _counts[label].GetValueOrDefault(token) + 1;
            _totals[label] = _totals.GetValueOrDefault(label) + 1;
        }
    }

    private static (string Text, string Label)? ParseCsvRow(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var separator = line.LastIndexOf("\",");
        if (separator < 2 || !line.StartsWith('"')) return null;
        var text = line[1..separator].Replace("\"\"", "\"");
        var label = line[(separator + 2)..].Trim();
        return (text, label);
    }

    public EmotionResult Analyze(string text)
    {
        var tokens = Tokenize(text).ToArray();
        var lowered = text.ToLowerInvariant();
        if (Regex.IsMatch(lowered, @"(?:а(?:х+а){2,}|х(?:а(?:х+а)+))")) return CreateResult("joy", .99);
        if (text.Trim().Length <= 12 && !Signals.Any(pair => pair.Value.Any(lowered.Contains)))
            return CreateResult("neutral", .92);
        var scores = new Dictionary<string, double>();
        foreach (var label in Labels)
        {
            var score = Math.Log(1d / Labels.Length);
            foreach (var token in tokens)
                score += Math.Log((_counts[label].GetValueOrDefault(token) + 1d) / (_totals[label] + _vocabulary.Count));
            score += Signals[label].Count(lowered.Contains) * 1.45;
            if (label == "surprise" && Regex.IsMatch(text, "[!?]{2,}")) score += .7;
            scores[label] = score;
        }
        var maximum = scores.Values.Max();
        var exponentials = scores.ToDictionary(pair => pair.Key, pair => Math.Exp(pair.Value - maximum));
        var sum = exponentials.Values.Sum();
        var best = exponentials.ToDictionary(pair => pair.Key, pair => pair.Value / sum).MaxBy(pair => pair.Value);
        return CreateResult(best.Key, best.Value);
    }

    public static EmotionResult CreateResult(string key, double confidence) => key switch
    {
        "joy" => new(key, "Радость", confidence, Color.FromRgb(255, 239, 184), Color.FromRgb(96, 70, 0)),
        "sadness" => new(key, "Грусть", confidence, Color.FromRgb(220, 234, 251), Color.FromRgb(38, 74, 112)),
        "anger" => new(key, "Злость", confidence, Color.FromRgb(255, 217, 207), Color.FromRgb(133, 48, 34)),
        "fear" => new(key, "Тревога", confidence, Color.FromRgb(233, 220, 247), Color.FromRgb(82, 51, 119)),
        "surprise" => new(key, "Удивление", confidence, Color.FromRgb(216, 240, 229), Color.FromRgb(20, 99, 72)),
        _ => new("neutral", "Нейтрально", confidence, Color.FromRgb(230, 234, 232), Color.FromRgb(67, 78, 75))
    };

    private static IEnumerable<string> Tokenize(string text)
    {
        var words = WordRegex().Matches(text.ToLowerInvariant()).Select(match => match.Value).Where(word => word.Length > 1).ToArray();
        foreach (var word in words) yield return word;
        for (var index = 0; index < words.Length - 1; index++) yield return $"{words[index]} {words[index + 1]}";
    }

    [GeneratedRegex("[а-яёa-z0-9]+", RegexOptions.IgnoreCase)]
    private static partial Regex WordRegex();
}
