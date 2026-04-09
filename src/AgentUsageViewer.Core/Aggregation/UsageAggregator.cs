using AgentUsageViewer.Core.Models;
using AgentUsageViewer.Core.Pricing;

namespace AgentUsageViewer.Core.Aggregation;

public sealed class UsageAggregator
{
    public UsageDashboardReport BuildReport(
        IEnumerable<UsageRecord> records,
        PricingTable pricingTable,
        TimeZoneInfo timeZone,
        DateTimeOffset now,
        bool claudeAvailable,
        bool codexAvailable)
    {
        var allRecords = records.ToList();

        var reports = new Dictionary<TimeRangeKind, RangeReport>
        {
            [TimeRangeKind.Today] = BuildRangeReport(TimeRangeKind.Today, allRecords, pricingTable, timeZone, now, claudeAvailable, codexAvailable),
            [TimeRangeKind.SevenDays] = BuildRangeReport(TimeRangeKind.SevenDays, allRecords, pricingTable, timeZone, now, claudeAvailable, codexAvailable),
            [TimeRangeKind.ThirtyDays] = BuildRangeReport(TimeRangeKind.ThirtyDays, allRecords, pricingTable, timeZone, now, claudeAvailable, codexAvailable),
            [TimeRangeKind.AllTime] = BuildRangeReport(TimeRangeKind.AllTime, allRecords, pricingTable, timeZone, now, claudeAvailable, codexAvailable),
        };

        return new UsageDashboardReport(reports);
    }

    private static RangeReport BuildRangeReport(
        TimeRangeKind range,
        IReadOnlyCollection<UsageRecord> allRecords,
        PricingTable pricingTable,
        TimeZoneInfo timeZone,
        DateTimeOffset now,
        bool claudeAvailable,
        bool codexAvailable)
    {
        var filtered = allRecords.Where(record => IsInRange(record, range, timeZone, now)).ToList();

        return new RangeReport(
            range,
            BuildAgentReport(AgentKind.Claude, filtered, pricingTable, timeZone, now, claudeAvailable),
            BuildAgentReport(AgentKind.Codex, filtered, pricingTable, timeZone, now, codexAvailable));
    }

    private static AgentUsageReport BuildAgentReport(
        AgentKind agent,
        IEnumerable<UsageRecord> records,
        PricingTable pricingTable,
        TimeZoneInfo timeZone,
        DateTimeOffset now,
        bool isAvailable)
    {
        var agentRecords = records.Where(record => record.Agent == agent).ToList();
        var sessionCount = agentRecords.Select(static record => record.SessionId).Distinct(StringComparer.Ordinal).Count();

        return new AgentUsageReport(
            agent,
            isAvailable,
            agentRecords.Sum(static record => record.Metrics.TotalTokens),
            SumCosts(agentRecords, pricingTable),
            sessionCount,
            BuildBreakdown(agentRecords, static record => record.Model ?? "Unknown model", pricingTable),
            BuildBreakdown(agentRecords, static record => record.Cwd ?? "Unknown project", pricingTable),
            BuildDailyTrend(agentRecords, pricingTable, timeZone, now));
    }

    private static IReadOnlyList<BreakdownItem> BuildBreakdown(
        IEnumerable<UsageRecord> records,
        Func<UsageRecord, string> keySelector,
        PricingTable pricingTable)
    {
        return records
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var grouped = group.ToList();
                return new BreakdownItem(
                    group.Key,
                    grouped.Sum(static record => record.Metrics.TotalTokens),
                    SumCosts(grouped, pricingTable),
                    grouped.Select(static record => record.SessionId).Distinct(StringComparer.Ordinal).Count());
            })
            .OrderByDescending(static item => item.TotalTokens)
            .Take(8)
            .ToList();
    }

    private static IReadOnlyList<DailyUsagePoint> BuildDailyTrend(
        IEnumerable<UsageRecord> records,
        PricingTable pricingTable,
        TimeZoneInfo timeZone,
        DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).Date);
        var grouped = records
            .GroupBy(record => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(record.TimestampUtc, timeZone).Date))
            .ToDictionary(group => group.Key, group => group.ToList());

        var points = new List<DailyUsagePoint>(14);

        for (var index = 13; index >= 0; index--)
        {
            var day = today.AddDays(-index);
            if (!grouped.TryGetValue(day, out var dayRecords))
            {
                points.Add(new DailyUsagePoint(day, 0, 0m));
                continue;
            }

            points.Add(new DailyUsagePoint(
                day,
                dayRecords.Sum(static record => record.Metrics.TotalTokens),
                SumCosts(dayRecords, pricingTable)));
        }

        return points;
    }

    private static decimal? SumCosts(IEnumerable<UsageRecord> records, PricingTable pricingTable)
    {
        decimal total = 0m;

        foreach (var record in records)
        {
            var cost = CostCalculator.Calculate(record, pricingTable);
            if (cost is null)
            {
                return null;
            }

            total += cost.Value;
        }

        return total;
    }

    private static bool IsInRange(UsageRecord record, TimeRangeKind range, TimeZoneInfo timeZone, DateTimeOffset now)
    {
        if (range == TimeRangeKind.AllTime)
        {
            return true;
        }

        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).Date);
        var recordDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(record.TimestampUtc, timeZone).Date);
        var dayOffset = localToday.DayNumber - recordDay.DayNumber;

        return range switch
        {
            TimeRangeKind.Today => dayOffset == 0,
            TimeRangeKind.SevenDays => dayOffset is >= 0 and < 7,
            TimeRangeKind.ThirtyDays => dayOffset is >= 0 and < 30,
            _ => true,
        };
    }
}
