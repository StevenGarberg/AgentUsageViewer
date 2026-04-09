namespace AgentUsageViewer.Core.Models;

public sealed record CodexRateLimitSnapshot(
    RateLimitWindow? Primary,
    RateLimitWindow? Secondary,
    string? PlanType);
