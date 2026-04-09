namespace AgentUsageViewer.Core.Models;

public sealed record UsageDashboardReport(
    IReadOnlyDictionary<TimeRangeKind, RangeReport> Ranges)
{
    public RangeReport GetRange(TimeRangeKind range) => Ranges[range];
}
