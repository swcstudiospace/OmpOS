using Autonogrammer.Harness.AgentLoop.Gsd;

namespace Autonogrammer.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// A runner that returns canned output per project directory and records every call, so tests count spawns without
/// starting a process.
/// </summary>
internal sealed class GsdFakeProcessRunner : IGsdProcessRunner
{
    private readonly Lock _gate = new();
    private readonly List<(string ToolsPath, string Query, string ProjectDirectory)> _calls = [];

    /// <summary>Gets or sets what a call does. It receives the project directory and returns the standard output or throws.</summary>
    public Func<string, string> Respond { get; set; } = static _ => "{}";

    /// <summary>Gets the number of calls so far.</summary>
    public int CallCount
    {
        get
        {
            lock (_gate)
            {
                return _calls.Count;
            }
        }
    }

    /// <summary>Gets a copy of the recorded calls.</summary>
    public IReadOnlyList<(string ToolsPath, string Query, string ProjectDirectory)> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <inheritdoc />
    public Task<string> QueryAsync(
        string gsdToolsPath,
        string queryName,
        IReadOnlyList<string> queryArguments,
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _calls.Add((gsdToolsPath, queryName, projectDirectory));
        }

        return Task.FromResult(Respond(projectDirectory));
    }
}
