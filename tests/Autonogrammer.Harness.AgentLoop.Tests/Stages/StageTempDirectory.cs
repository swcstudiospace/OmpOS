using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Launch;

namespace Autonogrammer.Harness.AgentLoop.Tests.Stages;

/// <summary>A temporary state root for stage observer tests, deleted on dispose, with helpers that build scopes and write the vendor files.</summary>
internal sealed class StageTempDirectory : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "autonogrammer-stage-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the root directory.</summary>
    public StageTempDirectory() => Directory.CreateDirectory(_root);

    /// <summary>Builds the scope of a session whose private state lives under this root.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="acpSessionId">The omp session identifier, or null before the engine reports it.</param>
    /// <param name="graphId">The Graph identifier, or null.</param>
    /// <returns>The scope.</returns>
    public StageObservationScope Scope(string sessionId = "s-1", string? acpSessionId = "01a17752-7e43-7002-8ac7-6781e5b19475", string? graphId = null) =>
        new()
        {
            SessionId = sessionId,
            WorkspaceRoot = _root,
            SessionStateDirectory = SessionStateLayout.SessionDirectory(_root, sessionId),
            AcpSessionId = acpSessionId,
            GraphId = graphId,
        };

    /// <summary>Writes the plan file of a scope.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="content">The file content.</param>
    public static void WritePlan(StageObservationScope scope, byte[] content)
    {
        var path = SessionStateLayout.UltrathinkSessionFile(scope.SessionStateDirectory, scope.AcpSessionId!);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    /// <summary>Writes the swarm live file of a scope.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="content">The file content.</param>
    public static void WriteLive(StageObservationScope scope, byte[] content)
    {
        var path = SessionStateLayout.SwarmLiveFile(scope.SessionStateDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    /// <summary>Replaces the swarm event log of a scope.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="content">The file content.</param>
    public static void WriteEvents(StageObservationScope scope, byte[] content)
    {
        var path = SessionStateLayout.SwarmEventsFile(scope.SessionStateDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    /// <summary>Appends to the swarm event log of a scope.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="content">The bytes to append.</param>
    public static void AppendEvents(StageObservationScope scope, byte[] content)
    {
        var path = SessionStateLayout.SwarmEventsFile(scope.SessionStateDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        stream.Write(content);
    }

    /// <summary>Returns the path of the swarm event log of a scope.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The path.</returns>
    public static string EventsPath(StageObservationScope scope) => SessionStateLayout.SwarmEventsFile(scope.SessionStateDirectory);

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp directory; a leftover directory is not a test failure.
        }
    }
}
