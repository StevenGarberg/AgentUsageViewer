namespace AgentUsageViewer.Core.IO;

public readonly record struct IncrementalReadResult(long ConsumedLength, int LinesRead);
