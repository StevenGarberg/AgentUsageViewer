namespace AgentUsageViewer.Core.Models;

public sealed class AppSettings
{
    public string ClaudeRoot { get; set; } = string.Empty;

    public string CodexRoot { get; set; } = string.Empty;

    public string PricingPath { get; set; } = "pricing.json";

    public WindowSettings Window { get; set; } = new();

    public int RefreshDebounceMs { get; set; } = 500;

    public bool ShowCost { get; set; } = true;

    public List<string> Ranges { get; set; } = ["today", "7d", "30d", "all"];

    public static AppSettings CreateDefault()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return new AppSettings
        {
            ClaudeRoot = Path.Combine(userProfile, ".claude", "projects"),
            CodexRoot = Path.Combine(userProfile, ".codex", "sessions"),
            PricingPath = "pricing.json",
            Window = new WindowSettings(),
            RefreshDebounceMs = 500,
            ShowCost = true,
            Ranges = ["today", "7d", "30d", "all"],
        };
    }
}
