namespace AgentUsageViewer.Core.Models;

public sealed record AgentUsageReport(
    AgentKind Agent,
    bool IsAvailable,
    long TotalTokens,
    decimal? TotalCost,
    int SessionCount,
    IReadOnlyList<BreakdownItem> ByModel,
    IReadOnlyList<BreakdownItem> ByProject,
    IReadOnlyList<DailyUsagePoint> DailyTrend);
