namespace Autonogrammer.Harness.BrowserUse.Options;

/// <summary>How a top-level origin other than the requested one is treated.</summary>
public enum PolicyMode
{
    /// <summary>Ask on each new origin.</summary>
    PromptOnNewOrigin = 0,

    /// <summary>Allow only listed hosts.</summary>
    Strict = 1,

    /// <summary>Skip the new-origin card. Dispatch turns treat this as <see cref="PromptOnNewOrigin"/>.</summary>
    Open = 2,
}
