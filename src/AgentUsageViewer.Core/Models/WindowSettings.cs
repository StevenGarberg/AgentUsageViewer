namespace AgentUsageViewer.Core.Models;

public sealed class WindowSettings
{
    public double? X { get; set; }

    public double? Y { get; set; }

    public double Opacity { get; set; } = 0.92d;

    public double? BreakdownX { get; set; }

    public double? BreakdownY { get; set; }
}
