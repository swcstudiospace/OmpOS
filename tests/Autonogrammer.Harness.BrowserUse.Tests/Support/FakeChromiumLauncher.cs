using Autonogrammer.Harness.BrowserUse.Chromium;

namespace Autonogrammer.Harness.BrowserUse.Tests.Support;

/// <summary>A launcher that records the request and returns a scripted session. It never starts a process.</summary>
internal sealed class FakeChromiumLauncher : IChromiumLauncher
{
    private readonly List<ChromiumLaunchRequest> _requests = [];
    private readonly List<FakeChromiumSession> _sessions = [];

    /// <summary>Gets a recorded request.</summary>
    internal ChromiumLaunchRequest RequestAt(int index)
    {
        return _requests[index];
    }

    /// <summary>Gets a created session.</summary>
    internal FakeChromiumSession SessionAt(int index)
    {
        return _sessions[index];
    }

    /// <inheritdoc />
    public Task<IChromiumSession> LaunchAsync(ChromiumLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _requests.Add(request);
        var session = new FakeChromiumSession(request.ProfileDirectory);
        _sessions.Add(session);
        return Task.FromResult<IChromiumSession>(session);
    }
}
