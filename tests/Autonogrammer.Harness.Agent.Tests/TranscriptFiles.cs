namespace Autonogrammer.Harness.Agent.Tests;

/// <summary>Reads the recorded transcripts linked into this test project's output folder.</summary>
internal static class TranscriptFiles
{
    /// <summary>Reads a transcript as its non-empty lines, in file order.</summary>
    /// <param name="fileName">The file name under <c>TestData/frames</c>.</param>
    public static List<string> ReadLines(string fileName) =>
        File.ReadAllLines(FullPath(fileName)).Where(static line => line.Length > 0).ToList();

    /// <summary>Reads a transcript sidecar as text.</summary>
    /// <param name="fileName">The file name under <c>TestData/frames</c>.</param>
    public static string ReadText(string fileName) => File.ReadAllText(FullPath(fileName));

    private static string FullPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "frames", fileName);
}
