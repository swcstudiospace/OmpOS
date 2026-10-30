namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>How a top-level origin other than the requested one is treated. Names match the options record the head stores.</summary>
public enum BrowserUsePolicyMode
{
    /// <summary>Ask on each new origin.</summary>
    PromptOnNewOrigin = 0,

    /// <summary>Allow only listed hosts.</summary>
    Strict = 1,

    /// <summary>Skip the new-origin card.</summary>
    Open = 2,
}
