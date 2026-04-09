namespace AgentUsageViewer.Core.Models;

public sealed record RangeReport(
    TimeRangeKind Range,
    AgentUsageReport Claude,
    AgentUsageReport Codex);
