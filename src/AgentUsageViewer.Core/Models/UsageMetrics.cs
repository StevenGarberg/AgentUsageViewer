namespace AgentUsageViewer.Core.Models;

public readonly record struct UsageMetrics(
    long InputTokens,
    long CacheWriteTokens,
    long CacheReadTokens,
    long CachedInputTokens,
    long OutputTokens,
    long ReasoningOutputTokens)
{
    public long TotalTokens =>
        InputTokens +
        CacheWriteTokens +
        CacheReadTokens +
        CachedInputTokens +
        OutputTokens +
        ReasoningOutputTokens;

    public UsageMetrics Add(UsageMetrics other) =>
        new(
            InputTokens + other.InputTokens,
            CacheWriteTokens + other.CacheWriteTokens,
            CacheReadTokens + other.CacheReadTokens,
            CachedInputTokens + other.CachedInputTokens,
            OutputTokens + other.OutputTokens,
            ReasoningOutputTokens + other.ReasoningOutputTokens);
}
