namespace AgentUsageViewer.Core.IO;

public readonly record struct FileCursor(long Length, DateTime LastWriteUtc);
