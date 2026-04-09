using System.Text.Json;
using AgentUsageViewer.Core.Models;

namespace AgentUsageViewer.Core.Configuration;

public sealed class SettingsStore
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public string SettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AgentUsageViewer");

    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        Directory.CreateDirectory(SettingsDirectory);

        if (!File.Exists(SettingsPath))
        {
            var defaults = AppSettings.CreateDefault();
            Save(defaults);
            return defaults;
        }

        AppSettings settings;
        try
        {
            using var stream = File.OpenRead(SettingsPath);
            settings = JsonSerializer.Deserialize<AppSettings>(stream, _serializerOptions) ?? AppSettings.CreateDefault();
        }
        catch (JsonException)
        {
            settings = AppSettings.CreateDefault();
            Save(settings);
        }

        var defaultsTemplate = AppSettings.CreateDefault();

        settings.ClaudeRoot = string.IsNullOrWhiteSpace(settings.ClaudeRoot) ? defaultsTemplate.ClaudeRoot : settings.ClaudeRoot;
        settings.CodexRoot = string.IsNullOrWhiteSpace(settings.CodexRoot) ? defaultsTemplate.CodexRoot : settings.CodexRoot;
        settings.PricingPath = string.IsNullOrWhiteSpace(settings.PricingPath) ? "pricing.json" : settings.PricingPath;
        settings.Window ??= new WindowSettings();
        settings.Ranges ??= ["today", "7d", "30d", "all"];

        return settings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        using var stream = File.Create(SettingsPath);
        JsonSerializer.Serialize(stream, settings, _serializerOptions);
    }

    public string ResolvePricingPath(AppSettings settings)
    {
        if (Path.IsPathFullyQualified(settings.PricingPath))
        {
            return settings.PricingPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, settings.PricingPath));
    }
}
