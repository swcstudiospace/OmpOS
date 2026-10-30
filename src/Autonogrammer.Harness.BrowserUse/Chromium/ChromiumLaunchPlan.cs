namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>Assembles the one Chromium argument list. Arguments are never joined into a shell string, and the sandbox flag is never added.</summary>
public static class ChromiumLaunchPlan
{
    /// <summary>
    /// Builds the flag list for one launch. Each flag appears once. <c>--remote-debugging-port</c> and <c>--no-sandbox</c> are never present.
    /// </summary>
    /// <param name="proxyPort">The loopback egress-guard port.</param>
    /// <param name="windowWidth">The window width in pixels.</param>
    /// <param name="windowHeight">The window height in pixels.</param>
    /// <param name="userDataDir">The host-owned profile directory.</param>
    /// <returns>The argument list, in a stable order.</returns>
    public static IReadOnlyList<string> Build(int proxyPort, int windowWidth, int windowHeight, string userDataDir)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(proxyPort);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowHeight);
        ArgumentException.ThrowIfNullOrEmpty(userDataDir);
        return
        [
            "--headless=new",
            "--remote-debugging-pipe",
            "--user-data-dir=" + userDataDir,
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-extensions",
            "--disable-sync",
            "--disable-background-networking",
            "--disable-component-update",
            "--mute-audio",
            "--hide-scrollbars",
            "--password-store=basic",
            "--use-mock-keychain",
            "--proxy-server=http://127.0.0.1:" + proxyPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--proxy-bypass-list=<-loopback>",
            "--force-webrtc-ip-handling-policy=disable_non_proxied_udp",
            "--window-size=" + windowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + windowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ];
    }
}
