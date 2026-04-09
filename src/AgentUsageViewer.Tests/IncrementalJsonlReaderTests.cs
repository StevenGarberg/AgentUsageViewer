using AgentUsageViewer.Core.IO;

namespace AgentUsageViewer.Tests;

public sealed class IncrementalJsonlReaderTests
{
    [Fact]
    public async Task Reader_OnlyEmitsNewlyCompletedLines()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "{\"a\":1}\n{\"b\":");

            var reader = new IncrementalJsonlReader();
            var seen = new List<string>();

            var first = await reader.ReadNewLinesAsync(
                tempFile,
                0,
                (line, isTail, _) =>
                {
                    if (isTail)
                    {
                        return new ValueTask<bool>(false);
                    }

                    seen.Add(line);
                    return new ValueTask<bool>(true);
                },
                CancellationToken.None);

            Assert.Single(seen);
            Assert.Equal("{\"a\":1}", seen[0]);

            await File.AppendAllTextAsync(tempFile, "2}\n");

            var second = await reader.ReadNewLinesAsync(
                tempFile,
                first.ConsumedLength,
                (line, _, _) =>
                {
                    seen.Add(line);
                    return new ValueTask<bool>(true);
                },
                CancellationToken.None);

            Assert.Equal(2, seen.Count);
            Assert.Equal("{\"b\":2}", seen[1]);
            Assert.True(second.ConsumedLength > first.ConsumedLength);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
