namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>Starts a host-owned Chromium and connects over a pipe. Tests substitute a fake.</summary>
public interface IChromiumLauncher
{
    /// <summary>Starts Chromium and connects.</summary>
    /// <param name="request">The launch request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connected session.</returns>
    Task<IChromiumSession> LaunchAsync(ChromiumLaunchRequest request, CancellationToken cancellationToken);
}
