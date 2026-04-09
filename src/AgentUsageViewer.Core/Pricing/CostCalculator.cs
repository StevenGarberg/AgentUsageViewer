using AgentUsageViewer.Core.Models;

namespace AgentUsageViewer.Core.Pricing;

public static class CostCalculator
{
    private const decimal OneMillion = 1_000_000m;

    public static decimal? Calculate(UsageRecord record, PricingTable table)
    {
        if (!table.TryGetEntry(record.Model, out var entry) || entry is null)
        {
            return null;
        }

        decimal total = 0m;

        if (!TryAdd(record.Metrics.InputTokens, entry.Input, ref total) ||
            !TryAdd(record.Metrics.CacheWriteTokens, entry.CacheWrite, ref total) ||
            !TryAdd(record.Metrics.CacheReadTokens, entry.CacheRead, ref total) ||
            !TryAdd(record.Metrics.CachedInputTokens, entry.CachedInput, ref total) ||
            !TryAdd(record.Metrics.OutputTokens + record.Metrics.ReasoningOutputTokens, entry.Output, ref total))
        {
            return null;
        }

        return total;
    }

    private static bool TryAdd(long tokens, decimal? ratePerMillion, ref decimal total)
    {
        if (tokens == 0)
        {
            return true;
        }

        if (ratePerMillion is null)
        {
            return false;
        }

        total += (tokens / OneMillion) * ratePerMillion.Value;
        return true;
    }
}
