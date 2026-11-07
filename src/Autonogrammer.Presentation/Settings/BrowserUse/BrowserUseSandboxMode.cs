namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>Whether Chromium's sandbox is required.</summary>
public enum BrowserUseSandboxMode
{
    /// <summary>The sandbox stays on.</summary>
    Required = 0,

    /// <summary>The sandbox is off. The head refuses this unless the test switch is set.</summary>
    Disabled = 1,
}
