namespace AgentUsageViewer.Core.Models;

public sealed record UsageRecord(
    AgentKind Agent,
    string SourceFile,
    string SessionId,
    string DedupKey,
    DateTimeOffset TimestampUtc,
    string? Cwd,
    string? Model,
    UsageMetrics Metrics);
