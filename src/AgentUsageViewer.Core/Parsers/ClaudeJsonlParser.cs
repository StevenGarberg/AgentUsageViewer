using System.Globalization;
using System.Text;
using System.Text.Json;
using AgentUsageViewer.Core.Models;

namespace AgentUsageViewer.Core.Parsers;

public sealed class ClaudeJsonlParser
{
    public bool TryParseLine(string line, string sourceFile, out UsageRecord? record)
    {
        record = null;

        try
        {
            var json = Encoding.UTF8.GetBytes(line);
            var reader = new Utf8JsonReader(json, isFinalBlock: true, state: default);

            string? type = null;
            string? timestamp = null;
            string? sessionId = null;
            string? cwd = null;
            string? model = null;
            string? messageId = null;
            var metrics = default(UsageMetrics);
            var hasUsage = false;

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var property = reader.GetString();
                reader.Read();

                switch (property)
                {
                    case "type":
                        type = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                        break;
                    case "timestamp":
                        timestamp = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                        break;
                    case "sessionId":
                        sessionId = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                        break;
                    case "cwd":
                        cwd = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                        break;
                    case "message":
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            ParseMessage(ref reader, ref model, ref messageId, ref metrics, ref hasUsage);
                        }
                        else
                        {
                            reader.Skip();
                        }

                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            if (!string.Equals(type, "assistant", StringComparison.Ordinal) ||
                !hasUsage ||
                string.IsNullOrWhiteSpace(timestamp) ||
                string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            var timestampUtc = DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            var dedupKey = string.IsNullOrWhiteSpace(messageId) ? $"{sessionId}:{timestamp}" : $"{sessionId}:{messageId}";

            record = new UsageRecord(
                AgentKind.Claude,
                sourceFile,
                sessionId,
                dedupKey,
                timestampUtc,
                cwd,
                model,
                metrics);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool LooksCompleteJson(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ParseMessage(
        ref Utf8JsonReader reader,
        ref string? model,
        ref string? messageId,
        ref UsageMetrics metrics,
        ref bool hasUsage)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var property = reader.GetString();
            reader.Read();

            switch (property)
            {
                case "model":
                    model = reader.TokenType == JsonTokenType.String ? reader.GetString() : model;
                    break;
                case "id":
                    messageId = reader.TokenType == JsonTokenType.String ? reader.GetString() : messageId;
                    break;
                case "usage":
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        metrics = ParseUsage(ref reader);
                        hasUsage = true;
                    }
                    else
                    {
                        reader.Skip();
                    }

                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
    }

    private static UsageMetrics ParseUsage(ref Utf8JsonReader reader)
    {
        long input = 0;
        long cacheWrite = 0;
        long cacheRead = 0;
        long output = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var property = reader.GetString();
            reader.Read();

            switch (property)
            {
                case "input_tokens":
                    input = ReadLong(ref reader);
                    break;
                case "cache_creation_input_tokens":
                    cacheWrite = ReadLong(ref reader);
                    break;
                case "cache_read_input_tokens":
                    cacheRead = ReadLong(ref reader);
                    break;
                case "output_tokens":
                    output = ReadLong(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new UsageMetrics(input, cacheWrite, cacheRead, 0, output, 0);
    }

    private static long ReadLong(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var number))
        {
            return number;
        }

        return reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), out number)
            ? number
            : 0;
    }
}
