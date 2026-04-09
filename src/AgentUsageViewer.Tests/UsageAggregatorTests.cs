using AgentUsageViewer.Core.Aggregation;
using AgentUsageViewer.Core.Models;
using AgentUsageViewer.Core.Pricing;

namespace AgentUsageViewer.Tests;

public sealed class UsageAggregatorTests
{
    [Fact]
    public void Aggregator_TodayUsesLocalTime()
    {
        var timezone = GetEasternTimeZone();
        var now = new DateTimeOffset(2026, 04, 08, 10, 00, 00, TimeSpan.FromHours(-4));
        var records = new[]
        {
            new UsageRecord(
                AgentKind.Claude,
                "a",
                "session-a",
                "a",
                new DateTimeOffset(2026, 04, 08, 01, 00, 00, TimeSpan.FromHours(-4)),
                "proj",
                "claude-opus-4-6",
                new UsageMetrics(100, 0, 0, 0, 10, 0)),
            new UsageRecord(
                AgentKind.Claude,
                "b",
                "session-b",
                "b",
                new DateTimeOffset(2026, 04, 07, 23, 30, 00, TimeSpan.FromHours(-4)),
                "proj",
                "claude-opus-4-6",
                new UsageMetrics(200, 0, 0, 0, 20, 0)),
        };

        var aggregator = new UsageAggregator();
        var report = aggregator.BuildReport(records, PricingTable.Empty, timezone, now, true, false);

        Assert.Equal(110, report.GetRange(TimeRangeKind.Today).Claude.TotalTokens);
        Assert.Equal(330, report.GetRange(TimeRangeKind.SevenDays).Claude.TotalTokens);
    }

    private static TimeZoneInfo GetEasternTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
    }
}
