namespace AgentUsageViewer.Core.Models;

public sealed record RateLimitWindow(
    double UsedPercent,
    int WindowMinutes,
    DateTimeOffset? ResetsAtUtc);
