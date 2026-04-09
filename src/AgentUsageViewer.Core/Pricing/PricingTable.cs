using System.Text.Json;

namespace AgentUsageViewer.Core.Pricing;

public sealed class PricingTable
{
    public static PricingTable Empty { get; } = new(new Dictionary<string, PricingEntry>(StringComparer.OrdinalIgnoreCase));

    public PricingTable(IReadOnlyDictionary<string, PricingEntry> models)
    {
        Models = models;
    }

    public IReadOnlyDictionary<string, PricingEntry> Models { get; }

    public bool TryGetEntry(string? model, out PricingEntry? entry)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            entry = null;
            return false;
        }

        return Models.TryGetValue(model, out entry);
    }

    public static PricingTable Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        using var stream = File.OpenRead(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, PricingEntry>>(stream, JsonOptions);
        return raw is null
            ? Empty
            : new PricingTable(new Dictionary<string, PricingEntry>(raw, StringComparer.OrdinalIgnoreCase));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
