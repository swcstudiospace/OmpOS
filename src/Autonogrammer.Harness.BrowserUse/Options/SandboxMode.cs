namespace Autonogrammer.Harness.BrowserUse.Options;

/// <summary>Whether Chromium's sandbox is required. Disabled is refused unless the test switch is set.</summary>
public enum SandboxMode
{
    /// <summary>The sandbox stays on. No <c>--no-sandbox</c> flag is ever added.</summary>
    Required = 0,

    /// <summary>Reserved for a root test host. The validator refuses this unless the allow switch is set.</summary>
    Disabled = 1,
}
