namespace Autonogrammer.Harness.Mcp.Tests;

/// <summary>A temporary file the fixture appends received tool calls to; deleted when the test ends.</summary>
internal sealed class CallLogFile : IDisposable
{
    /// <summary>Creates a path for a new, empty call log.</summary>
    public CallLogFile()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "autonogrammer-mcp-" + Guid.NewGuid().ToString("N") + ".log");
    }

    /// <summary>The log file path.</summary>
    public string Path { get; }

    /// <summary>The tool names the fixture has received, in order; empty when nothing reached it.</summary>
    public IReadOnlyList<string> ReceivedTools =>
        File.Exists(Path) ? File.ReadAllLines(Path) : Array.Empty<string>();

    /// <inheritdoc />
    public void Dispose()
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
    }
}
