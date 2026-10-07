using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace TelegramMood;

public partial class MainWindow : Window
{
    private readonly EmotionAnalyzer _analyzer;
    private readonly ObservableCollection<AnalyzedMessage> _messages = [];

    public MainWindow()
    {
        InitializeComponent();
        _analyzer = EmotionAnalyzer.FromCsv(
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-labeled-52.csv"),
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-surprise-labeled.csv"),
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-fear-labeled.csv"),
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-sadness-labeled.csv"),
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-sadness-round2.csv"),
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-fear-round2.csv"),
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-sadness-round3.csv"),
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-surprise-round2.csv"),
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-sadness-round4.csv"),
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-fear-round3.csv"),
            Path.Combine(AppContext.BaseDirectory, "data", "moodlens-anger-labeled.csv"));
        MessagesList.ItemsSource = _messages;
        LoadMessages(MessageData.Demo.Select(text => new MessageData.ImportedMessage(text)), "Демонстрационные данные");
    }

    private void DemoButton_Click(object sender, RoutedEventArgs e) => LoadMessages(MessageData.Demo.Select(text => new MessageData.ImportedMessage(text)), "Демонстрационные данные");

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var messages = MessageData.LoadFile();
            if (messages.Length == 0) return;
            LoadMessages(messages, "Загружен экспортированный чат");
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Не удалось прочитать экспортированный чат. Проверьте формат JSON/HTML/CSV.\n\n{exception.Message}", "Ошибка чтения", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadMessages(IEnumerable<MessageData.ImportedMessage> texts, string source)
    {
        _messages.Clear();
        var now = DateTime.Now;
        var imported = texts.Where(item => !string.IsNullOrWhiteSpace(item.Text)).ToArray();
        foreach (var (item, index) in imported.Select((item, index) => (item, index)))
        {
            _messages.Add(new AnalyzedMessage
            {
                Text = item.Text.Trim(),
                Time = item.Time ?? now.AddMinutes(index - imported.Length),
                Emotion = _analyzer.Analyze(item.Text)
            });
        }
        SourceStatus.Text = source;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        MessageCount.Text = $"{_messages.Count} {Pluralize(_messages.Count, "сообщение", "сообщения", "сообщений")}";
        if (_messages.Count == 0) return;

        var latest = _messages[^1].Emotion;
        CurrentMood.Text = latest.Name;
        MoodExplanation.Text = "Основная эмоция последнего сообщения в наборе";
        ConfidenceText.Text = $"Уверенность модели: {latest.Confidence:P0}";
        ConfidencePill.Background = new SolidColorBrush(latest.Background);
        ConfidenceText.Foreground = new SolidColorBrush(latest.Foreground);

        var groups = _messages.GroupBy(item => item.Emotion.Key).ToDictionary(group => group.Key, group => group.Count());
        var dominant = groups.MaxBy(pair => pair.Value).Key;
        TrendTitle.Text = $"Чаще всего — {EmotionAnalyzer.CreateResult(dominant, 0).Name.ToLowerInvariant()}";
        MoodBars.ItemsSource = new[] { "joy", "neutral", "sadness", "anger", "fear", "surprise" }
            .Select(key =>
            {
                var emotion = EmotionAnalyzer.CreateResult(key, 0);
                return new MoodBarItem { Name = emotion.Name, Percent = groups.GetValueOrDefault(key) * 100d / _messages.Count, Brush = new SolidColorBrush(emotion.Foreground) };
            }).Where(item => item.Percent > 0).ToArray();
    }

    private static string Pluralize(int count, string one, string few, string many)
    {
        var mod100 = count % 100; var mod10 = count % 10;
        return mod100 is >= 11 and <= 19 ? many : mod10 == 1 ? one : mod10 is >= 2 and <= 4 ? few : many;
    }
}
