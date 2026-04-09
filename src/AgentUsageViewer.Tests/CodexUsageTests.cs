using AgentUsageViewer.Core.Sources;

namespace AgentUsageViewer.Tests;

public sealed class CodexUsageTests
{
    [Fact]
    public async Task CodexSource_UsesMaxCumulativeTotalPerFile()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "2026", "03", "14"));
            var target = Path.Combine(nested.FullName, "rollout-2026-03-14T04-00-15-019cea80-5c50-7522-88fa-d0a6c08a5409.jsonl");
            File.Copy(TestPaths.Sample("codex", "cumulative-session.jsonl"), target, overwrite: true);

            await using var source = new CodexUsageSource(root.FullName, 100);
            await source.StartAsync();

            var record = Assert.Single(source.GetSnapshot());
            Assert.Equal("gpt-5.4", record.Model);
            Assert.Equal(1800, record.Metrics.TotalTokens);
            Assert.Equal("019cea80-5c50-7522-88fa-d0a6c08a5409", record.SessionId);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task CodexSource_FirstTurnContextModelWins()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "2026", "03", "15"));
            var target = Path.Combine(nested.FullName, "rollout-2026-03-15T09-00-00-019cea90-2222-7333-b1c8-55acf3f49999.jsonl");
            File.Copy(TestPaths.Sample("codex", "multi-turn-context.jsonl"), target, overwrite: true);

            await using var source = new CodexUsageSource(root.FullName, 100);
            await source.StartAsync();

            var record = Assert.Single(source.GetSnapshot());
            Assert.Equal("gpt-5.4", record.Model);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
