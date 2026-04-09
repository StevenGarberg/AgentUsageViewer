using AgentUsageViewer.Core.Models;

namespace AgentUsageViewer.Core.Parsers;

public sealed record CodexLineEvent(
    DateTimeOffset? TimestampUtc,
    string? SessionId,
    string? Cwd,
    string? Model,
    UsageMetrics? Metrics,
    long? ReportedTotalTokens);
