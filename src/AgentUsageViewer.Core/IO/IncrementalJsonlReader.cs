using System.Text;

namespace AgentUsageViewer.Core.IO;

public sealed class IncrementalJsonlReader
{
    public async Task<IncrementalReadResult> ReadNewLinesAsync(
        string path,
        long offset,
        Func<string, bool, CancellationToken, ValueTask<bool>> processLineAsync,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(path);

        if (!fileInfo.Exists)
        {
            return new IncrementalReadResult(offset, 0);
        }

        if (fileInfo.Length < offset)
        {
            offset = 0;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        stream.Seek(offset, SeekOrigin.Begin);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrEmpty(text))
        {
            return new IncrementalReadResult(offset, 0);
        }

        var consumedChars = 0;
        var linesRead = 0;
        var lineStart = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            var sliceLength = index - lineStart;
            var line = NormalizeLine(text.AsSpan(lineStart, sliceLength));

            if (!string.IsNullOrWhiteSpace(line))
            {
                await processLineAsync(line, false, cancellationToken);
                linesRead++;
            }

            consumedChars = index + 1;
            lineStart = index + 1;
        }

        if (lineStart < text.Length)
        {
            var tail = NormalizeLine(text.AsSpan(lineStart));
            if (string.IsNullOrWhiteSpace(tail))
            {
                consumedChars = text.Length;
            }
            else if (await processLineAsync(tail, true, cancellationToken))
            {
                consumedChars = text.Length;
                linesRead++;
            }
        }

        var consumedText = text[..consumedChars];
        var consumedBytes = Encoding.UTF8.GetByteCount(consumedText);

        return new IncrementalReadResult(offset + consumedBytes, linesRead);
    }

    private static string NormalizeLine(ReadOnlySpan<char> line)
    {
        return line.EndsWith("\r".AsSpan(), StringComparison.Ordinal)
            ? line[..^1].ToString()
            : line.ToString();
    }
}
