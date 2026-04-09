using AgentUsageViewer.Core.Models;

namespace AgentUsageViewer.Core.Sources;

public interface IUsageSource : IAsyncDisposable
{
    event EventHandler? SnapshotChanged;

    bool IsAvailable { get; }

    string RootPath { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<UsageRecord> GetSnapshot();
}
