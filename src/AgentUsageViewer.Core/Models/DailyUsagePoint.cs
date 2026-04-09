namespace AgentUsageViewer.Core.Models;

public sealed record DailyUsagePoint(
    DateOnly Day,
    long TotalTokens,
    decimal? TotalCost);
