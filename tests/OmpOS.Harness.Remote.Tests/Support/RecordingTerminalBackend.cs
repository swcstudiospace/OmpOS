using OmpOS.Harness.Terminal;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>A terminal backend that starts nothing: it records the options it was asked to start, so a test can check what would have been spawned.</summary>
internal sealed class RecordingTerminalBackend : ITerminalBackend
{
    /// <summary>Gets the options of every start request, in order.</summary>
    public List<TerminalSessionOptions> Started { get; } = [];

    /// <inheritdoc />
    public Task<ITerminalSession> StartAsync(TerminalSessionOptions options, CancellationToken cancellationToken)
    {
        Started.Add(options);
        return Task.FromResult<ITerminalSession>(new IdleTerminalSession());
    }
}
