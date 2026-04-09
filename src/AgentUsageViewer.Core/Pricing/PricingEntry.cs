namespace AgentUsageViewer.Core.Pricing;

public sealed class PricingEntry
{
    public decimal? Input { get; set; }

    public decimal? CacheWrite { get; set; }

    public decimal? CacheRead { get; set; }

    public decimal? CachedInput { get; set; }

    public decimal? Output { get; set; }
}
