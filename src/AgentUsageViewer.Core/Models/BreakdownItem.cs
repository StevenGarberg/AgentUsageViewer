namespace AgentUsageViewer.Core.Models;

public sealed record BreakdownItem(
    string Key,
    long TotalTokens,
    decimal? TotalCost,
    int SessionCount);
