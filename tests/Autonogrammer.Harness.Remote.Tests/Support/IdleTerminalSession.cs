using Autonogrammer.Harness.Terminal;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>A terminal session that does nothing: the stand-in the recording backend returns.</summary>
internal sealed class IdleTerminalSession : ITerminalSession
{
    /// <inheritdoc />
    public Task WriteInputAsync(string text, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Resize(int columns, int rows)
    {
    }

    /// <inheritdoc />
    public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
