using Autonogrammer.Harness.BrowserUse;
using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Egress;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using Autonogrammer.Harness.BrowserUse.Tools;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// The production composition of the browser page source: one <see cref="BrowserSessionHost"/> over the given launcher, the system
/// resolver that every egress proxy decides and dials with, and the origin grants the caller supplies. The caller owns the host.
/// </summary>
internal static class ProductionBrowserPages
{
    /// <summary>Creates the page source and the host that owns its browsers.</summary>
    /// <param name="options">The bound options. The host and every egress proxy read their bounds from it.</param>
    /// <param name="executablePath">The located Chromium binary.</param>
    /// <param name="grantsFor">Returns the origin grants for a Cowork session id.</param>
    /// <param name="launcher">The launcher. The desktop passes the Chromium process launcher.</param>
    /// <returns>The page source and its host.</returns>
    public static (IBrowserPageSource Pages, BrowserSessionHost Host) Create(
        BrowserUseOptions options,
        string executablePath,
        Func<string, OriginGrants> grantsFor,
        IChromiumLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        ArgumentNullException.ThrowIfNull(grantsFor);
        ArgumentNullException.ThrowIfNull(launcher);
        var host = new BrowserSessionHost(launcher, options);
        var pages = new ProductionBrowserPageSource(host, executablePath, grantsFor, new SystemHostResolver(), options);
        return (pages, host);
    }
}
