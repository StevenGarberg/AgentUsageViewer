using System.Collections.Concurrent;
using System.Threading.Channels;
using AgentUsageViewer.Core.IO;
using AgentUsageViewer.Core.Models;
using AgentUsageViewer.Core.Parsers;

namespace AgentUsageViewer.Core.Sources;

public sealed class ClaudeUsageSource : IUsageSource
{
    private readonly Lock _gate = new();
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debouncers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClaudeFileState> _fileStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileCursor> _cursors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ClaudeJsonlParser _parser = new();
    private readonly IncrementalJsonlReader _reader = new();
    private readonly TimeSpan _debounce;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    public ClaudeUsageSource(string rootPath, int debounceMs)
    {
        RootPath = rootPath;
        _debounce = TimeSpan.FromMilliseconds(Math.Max(100, debounceMs));
    }

    public event EventHandler? SnapshotChanged;

    public bool IsAvailable => Directory.Exists(RootPath);

    public string RootPath { get; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        foreach (var file in Directory.EnumerateFiles(RootPath, "*.jsonl", SearchOption.AllDirectories))
        {
            await ProcessFileAsync(file, _cts.Token);
        }

        StartWatcher();
        _processingTask = Task.Run(() => ProcessLoopAsync(_cts.Token), _cts.Token);
    }

    public IReadOnlyList<UsageRecord> GetSnapshot()
    {
        lock (_gate)
        {
            return _fileStates.Values
                .SelectMany(static state => state.Records)
                .OrderBy(static record => record.TimestampUtc)
                .ToList();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _channel.Writer.TryComplete();
        }

        if (_processingTask is not null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _watcher?.Dispose();

        foreach (var debouncer in _debouncers.Values)
        {
            debouncer.Dispose();
        }

        _cts?.Dispose();
    }

    private void StartWatcher()
    {
        _watcher = new FileSystemWatcher(RootPath, "*.jsonl")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        _watcher.Created += OnFileChanged;
        _watcher.Changed += OnFileChanged;
        _watcher.Renamed += (_, args) => Debounce(args.FullPath);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs args) => Debounce(args.FullPath);

    private void Debounce(string path)
    {
        var debouncer = _debouncers.AddOrUpdate(
            path,
            _ => new CancellationTokenSource(),
            (_, existing) =>
            {
                existing.Cancel();
                existing.Dispose();
                return new CancellationTokenSource();
            });

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounce, debouncer.Token);
                await _channel.Writer.WriteAsync(path, debouncer.Token);
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private async Task ProcessLoopAsync(CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var path))
            {
                if (File.Exists(path))
                {
                    await ProcessFileAsync(path, cancellationToken);
                }
            }
        }
    }

    private async Task ProcessFileAsync(string path, CancellationToken cancellationToken)
    {
        ClaudeFileState state;
        FileCursor cursor;
        var fileInfo = new FileInfo(path);

        lock (_gate)
        {
            _cursors.TryGetValue(path, out cursor);
            if (fileInfo.Exists && fileInfo.Length < cursor.Length)
            {
                cursor = default;
                _fileStates[path] = new ClaudeFileState();
            }

            if (!_fileStates.TryGetValue(path, out state!))
            {
                state = new ClaudeFileState();
                _fileStates[path] = state;
            }
        }

        var result = await _reader.ReadNewLinesAsync(
            path,
            cursor.Length,
            (line, isTail, _) =>
            {
                if (!_parser.TryParseLine(line, path, out var record) || record is null)
                {
                    return new ValueTask<bool>(isTail ? _parser.LooksCompleteJson(line) : true);
                }

                lock (_gate)
                {
                    if (state.DedupKeys.Add(record.DedupKey))
                    {
                        state.Records.Add(record);
                    }
                }

                return new ValueTask<bool>(true);
            },
            cancellationToken);

        lock (_gate)
        {
            _cursors[path] = new FileCursor(result.ConsumedLength, fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.UtcNow);
        }

        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class ClaudeFileState
    {
        public HashSet<string> DedupKeys { get; } = new(StringComparer.Ordinal);

        public List<UsageRecord> Records { get; } = [];
    }
}
