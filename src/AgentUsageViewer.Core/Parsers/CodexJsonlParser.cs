using System.Globalization;
using System.Text;
using System.Text.Json;
using AgentUsageViewer.Core.Models;

namespace AgentUsageViewer.Core.Parsers;

public sealed class CodexJsonlParser
{
    public bool TryParseLine(string line, string sourceFile, out CodexLineEvent? parsed)
    {
        parsed = null;

        try
        {
            var json = Encoding.UTF8.GetBytes(line);
            var reader = new Utf8JsonReader(json, isFinalBlock: true, state: default);

            string? type = null;
            string? timestamp = null;
            string? sessionId = null;
            string? cwd = null;
            string? model = null;
            UsageMetrics? metrics = null;
            long? totalTokens = null;
            CodexRateLimitSnapshot? rateLimits = null;

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
                    case "payload":
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            ParsePayload(ref reader, type, ref sessionId, ref cwd, ref model, ref metrics, ref totalTokens, ref rateLimits);
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

            if (sessionId is null && !TryGetSessionIdFromPath(sourceFile, out sessionId) && metrics is null && model is null && cwd is null)
            {
                return false;
            }

            parsed = new CodexLineEvent(
                string.IsNullOrWhiteSpace(timestamp)
                    ? null
                    : DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                sessionId,
                cwd,
                model,
                metrics,
                totalTokens,
                rateLimits);

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

    private static void ParsePayload(
        ref Utf8JsonReader reader,
        string? rootType,
        ref string? sessionId,
        ref string? cwd,
        ref string? model,
        ref UsageMetrics? metrics,
        ref long? totalTokens,
        ref CodexRateLimitSnapshot? rateLimits)
    {
        string? nestedType = null;

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
                case "id" when string.Equals(rootType, "session_meta", StringComparison.Ordinal):
                    sessionId = reader.TokenType == JsonTokenType.String ? reader.GetString() : sessionId;
                    break;
                case "cwd":
                    cwd = reader.TokenType == JsonTokenType.String ? reader.GetString() : cwd;
                    break;
                case "model":
                    model = reader.TokenType == JsonTokenType.String ? reader.GetString() : model;
                    break;
                case "type":
                    nestedType = reader.TokenType == JsonTokenType.String ? reader.GetString() : nestedType;
                    break;
                case "turn_id" when string.Equals(rootType, "turn_context", StringComparison.Ordinal):
                    if (reader.TokenType == JsonTokenType.String && string.IsNullOrWhiteSpace(sessionId))
                    {
                        sessionId = reader.GetString();
                    }

                    break;
                case "info" when string.Equals(rootType, "event_msg", StringComparison.Ordinal) &&
                                 string.Equals(nestedType, "token_count", StringComparison.Ordinal):
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        ParseTokenInfo(ref reader, ref metrics, ref totalTokens);
                    }
                    else
                    {
                        reader.Skip();
                    }

                    break;
                case "rate_limits" when string.Equals(rootType, "event_msg", StringComparison.Ordinal) &&
                                        string.Equals(nestedType, "token_count", StringComparison.Ordinal):
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        rateLimits = ParseRateLimits(ref reader);
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

    private static void ParseTokenInfo(
        ref Utf8JsonReader reader,
        ref UsageMetrics? metrics,
        ref long? totalTokens)
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

            if (property == "total_token_usage" && reader.TokenType == JsonTokenType.StartObject)
            {
                ParseTotalTokenUsage(ref reader, ref metrics, ref totalTokens);
                continue;
            }

            reader.Skip();
        }
    }

    private static void ParseTotalTokenUsage(
        ref Utf8JsonReader reader,
        ref UsageMetrics? metrics,
        ref long? totalTokens)
    {
        long input = 0;
        long cachedInput = 0;
        long output = 0;
        long reasoning = 0;
        long total = 0;

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
                case "cached_input_tokens":
                    cachedInput = ReadLong(ref reader);
                    break;
                case "output_tokens":
                    output = ReadLong(ref reader);
                    break;
                case "reasoning_output_tokens":
                    reasoning = ReadLong(ref reader);
                    break;
                case "total_tokens":
                    total = ReadLong(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        metrics = new UsageMetrics(input, 0, 0, cachedInput, output, reasoning);
        totalTokens = total == 0 ? metrics.Value.TotalTokens : total;
    }

    private static CodexRateLimitSnapshot ParseRateLimits(ref Utf8JsonReader reader)
    {
        RateLimitWindow? primary = null;
        RateLimitWindow? secondary = null;
        string? planType = null;

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
                case "primary" when reader.TokenType == JsonTokenType.StartObject:
                    primary = ParseRateLimitWindow(ref reader);
                    break;
                case "secondary" when reader.TokenType == JsonTokenType.StartObject:
                    secondary = ParseRateLimitWindow(ref reader);
                    break;
                case "plan_type":
                    planType = reader.TokenType == JsonTokenType.String ? reader.GetString() : planType;
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new CodexRateLimitSnapshot(primary, secondary, planType);
    }

    private static RateLimitWindow ParseRateLimitWindow(ref Utf8JsonReader reader)
    {
        double usedPercent = 0d;
        var windowMinutes = 0;
        DateTimeOffset? resetsAtUtc = null;

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
                case "used_percent":
                    usedPercent = ReadDouble(ref reader);
                    break;
                case "window_minutes":
                    windowMinutes = (int)ReadLong(ref reader);
                    break;
                case "resets_at":
                    var seconds = ReadLong(ref reader);
                    resetsAtUtc = seconds > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                        : null;
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new RateLimitWindow(usedPercent, windowMinutes, resetsAtUtc);
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

    private static double ReadDouble(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var value))
        {
            return value;
        }

        return reader.TokenType == JsonTokenType.String && double.TryParse(reader.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            ? value
            : 0d;
    }

    private static bool TryGetSessionIdFromPath(string sourceFile, out string? sessionId)
    {
        sessionId = null;
        var fileName = Path.GetFileNameWithoutExtension(sourceFile);

        const string marker = "T";
        const string prefix = "rollout-";

        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !fileName.Contains(marker, StringComparison.Ordinal))
        {
            return false;
        }

        var timestampSeparator = fileName.IndexOf(marker, StringComparison.Ordinal);
        var suffixStart = fileName.IndexOf('-', timestampSeparator);
        if (suffixStart < 0)
        {
            return false;
        }

        suffixStart = fileName.IndexOf('-', suffixStart + 1);
        if (suffixStart < 0)
        {
            return false;
        }

        suffixStart = fileName.IndexOf('-', suffixStart + 1);
        if (suffixStart < 0 || suffixStart + 1 >= fileName.Length)
        {
            return false;
        }

        sessionId = fileName[(suffixStart + 1)..];
        return !string.IsNullOrWhiteSpace(sessionId);
    }
}
