using AgentUsageViewer.Core.Parsers;
using AgentUsageViewer.Core.Sources;

namespace AgentUsageViewer.Tests;

public sealed class ClaudeUsageTests
{
    [Fact]
    public void ClaudeParser_ParsesAssistantUsageAndSkipsOtherLines()
    {
        var parser = new ClaudeJsonlParser();
        var lines = File.ReadAllLines(TestPaths.Sample("claude", "assistant-usage.jsonl"));

        var records = lines
            .Select(line => parser.TryParseLine(line, "sample.jsonl", out var record) ? record : null)
            .Where(record => record is not null)
            .ToList();

        var record = Assert.Single(records)!;
        Assert.Equal("claude-opus-4-6", record.Model);
        Assert.Equal(3, record.Metrics.InputTokens);
        Assert.Equal(40467, record.Metrics.CacheWriteTokens);
        Assert.Equal(7863, record.Metrics.CacheReadTokens);
        Assert.Equal(2, record.Metrics.OutputTokens);
    }

    [Fact]
    public async Task ClaudeSource_DeduplicatesResumedSessionByMessageId()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var target = Path.Combine(root.FullName, "resume-session.jsonl");
            File.Copy(TestPaths.Sample("claude", "resumed-session.jsonl"), target, overwrite: true);

            await using var source = new ClaudeUsageSource(root.FullName, 100);
            await source.StartAsync();

            var snapshot = source.GetSnapshot();
            Assert.Equal(2, snapshot.Count);
            Assert.Equal(130, snapshot.Sum(static record => record.Metrics.TotalTokens));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
