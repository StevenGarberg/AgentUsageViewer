namespace AgentUsageViewer.Tests;

internal static class TestPaths
{
    public static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static string Sample(params string[] parts) =>
        Path.Combine(new[] { RepoRoot, "samples" }.Concat(parts).ToArray());
}
