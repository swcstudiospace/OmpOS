using Autonogrammer.Harness.BrowserUse.Chromium;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tests.Support;

/// <summary>A launcher that records the request and returns a scripted session. It never starts a process.</summary>
internal sealed class FakeChromiumLauncher : IChromiumLauncher
{
    private readonly List<ChromiumLaunchRequest> _requests = [];
    private readonly List<FakeChromiumSession> _sessions = [];

    /// <summary>Gets or sets the page factory handed to every session this launcher creates. Null makes page opens fail.</summary>
    internal Func<ICDPSession>? PageFactory { get; set; }

    /// <summary>Gets how many launches were requested.</summary>
    internal int Launches => _requests.Count;

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
        var session = new FakeChromiumSession(request.ProfileDirectory) { PageFactory = PageFactory };
        _sessions.Add(session);
        return Task.FromResult<IChromiumSession>(session);
    }
}
