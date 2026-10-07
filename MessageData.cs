using Microsoft.Win32;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TelegramMood;

public static class MessageData
{
    public sealed record ImportedMessage(string Text, DateTime? Time = null);

    public static readonly string[] Demo =
    [
        "Ура, я наконец закончил этот проект!",
        "Сегодня было довольно спокойно, обычный день.",
        "Я волнуюсь перед завтрашней контрольной",
        "Ого, ты правда получил первое место?",
        "Меня ужасно бесит, когда так поступают",
        "Мне немного грустно, что каникулы закончились",
        "Встреча состоится в пятницу в 16:00",
        "Спасибо тебе, это было очень приятно!"
    ];

    public static ImportedMessage[] LoadFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите обезличенный файл чата",
            Filter = "Данные чата (*.json;*.html;*.htm;*.csv;*.txt)|*.json;*.html;*.htm;*.csv;*.txt|JSON (*.json)|*.json|HTML (*.html;*.htm)|*.html;*.htm|CSV (*.csv)|*.csv|Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return [];

        return Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
        {
            ".json" => LoadTelegramJson(dialog.FileName),
            ".html" or ".htm" => LoadTelegramHtml(dialog.FileName),
            _ => LoadCsv(dialog.FileName)
        };
    }

    private static ImportedMessage[] LoadCsv(string path)
    {
        var lines = File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length == 0) return [];

        var start = lines[0].Contains("text", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var messages = new List<ImportedMessage>();
        for (var index = start; index < lines.Length; index++)
        {
            var columns = ParseCsvLine(lines[index]);
            if (columns.Count > 0 && !string.IsNullOrWhiteSpace(columns[0])) messages.Add(new ImportedMessage(columns[0].Trim()));
        }
        return messages.ToArray();
    }

    private static ImportedMessage[] LoadTelegramJson(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("messages", out var messagesElement) || messagesElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("В JSON не найден массив messages. Выберите файл result.json из экспорта Telegram.");

        var messages = new List<ImportedMessage>();
        foreach (var item in messagesElement.EnumerateArray())
        {
            if (item.TryGetProperty("type", out var type) && type.GetString() != "message") continue;
            if (!item.TryGetProperty("text", out var textElement)) continue;
            var text = ExtractTelegramText(textElement).Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            DateTime? date = null;
            if (item.TryGetProperty("date", out var dateElement) && DateTime.TryParse(dateElement.GetString(), out var parsed)) date = parsed.ToLocalTime();
            messages.Add(new ImportedMessage(text, date));
        }
        return messages.ToArray();
    }

    private static ImportedMessage[] LoadTelegramHtml(string path)
    {
        var html = File.ReadAllText(path);
        var messages = new List<ImportedMessage>();
        var blocks = Regex.Matches(html, "<div[^>]*class=[\\\"']text[\\\"'][^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match block in blocks)
        {
            var text = WebUtility.HtmlDecode(StripHtml(block.Groups[1].Value)).Trim();
            if (!string.IsNullOrWhiteSpace(text)) messages.Add(new ImportedMessage(text));
        }
        if (messages.Count == 0) throw new InvalidDataException("В HTML не найдены текстовые сообщения Telegram.");
        return messages.ToArray();
    }

    private static string StripHtml(string html)
    {
        html = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        return Regex.Replace(html, "<[^>]+>", "");
    }

    private static string ExtractTelegramText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? "";
        if (element.ValueKind != JsonValueKind.Array) return "";

        var builder = new System.Text.StringBuilder();
        foreach (var fragment in element.EnumerateArray())
        {
            if (fragment.ValueKind == JsonValueKind.String) builder.Append(fragment.GetString());
            else if (fragment.ValueKind == JsonValueKind.Object && fragment.TryGetProperty("text", out var fragmentText) && fragmentText.ValueKind == JsonValueKind.String)
                builder.Append(fragmentText.GetString());
        }
        return builder.ToString();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"') { current.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(character);
        }
        result.Add(current.ToString());
        return result;
    }
}
