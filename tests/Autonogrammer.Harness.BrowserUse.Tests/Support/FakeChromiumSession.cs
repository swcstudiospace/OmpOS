using Autonogrammer.Harness.BrowserUse.Chromium;

namespace Autonogrammer.Harness.BrowserUse.Tests.Support;

/// <summary>A scripted browser session used by the session-host tests.</summary>
internal sealed class FakeChromiumSession : IChromiumSession
{
    private int _steps;

    /// <summary>Initializes a session for a profile the host already created.</summary>
    /// <param name="profileDirectory">The profile directory.</param>
    internal FakeChromiumSession(string profileDirectory)
    {
        ProfileDirectory = profileDirectory;
    }

    /// <inheritdoc />
    public int ProcessId { get; set; } = 4242;

    /// <inheritdoc />
    public string ProfileDirectory { get; }

    /// <summary>Gets or sets the working set the host reads.</summary>
    public long WorkingSetBytes { get; set; }

    /// <inheritdoc />
    public int TabCount { get; private set; }

    /// <summary>Gets the close order. CDP is 1 when it ran first.</summary>
    public int CdpOrder { get; private set; }

    /// <summary>Gets the kill order.</summary>
    public int KillOrder { get; private set; }

    /// <inheritdoc />
    public Task OpenTabAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TabCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CloseViaCdpAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CdpOrder = ++_steps;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("Chrome/149.0.0.0");
    }

    /// <inheritdoc />
    public Task CaptureOneFrameAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void KillProcessTree()
    {
        KillOrder = ++_steps;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
