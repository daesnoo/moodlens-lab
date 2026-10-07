using System.Windows.Media;

namespace TelegramMood;

public sealed class AnalyzedMessage
{
    public required string Text { get; init; }
    public required DateTime Time { get; init; }
    public required EmotionResult Emotion { get; init; }
    public string TimeText => Time.ToString("HH:mm");
    public string EmotionName => Emotion.Name;
    public Brush EmotionBackground => new SolidColorBrush(Emotion.Background);
    public Brush EmotionForeground => new SolidColorBrush(Emotion.Foreground);
}

public sealed record EmotionResult(string Key, string Name, double Confidence, Color Background, Color Foreground);

public sealed class MoodBarItem
{
    public required string Name { get; init; }
    public required double Percent { get; init; }
    public required Brush Brush { get; init; }
    public string PercentText => $"{Percent:0}%";
}
