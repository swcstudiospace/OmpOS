namespace Autonogrammer.Presentation.Hooks;

/// <summary>The one state the Hooks page shows in its main area.</summary>
public enum HooksContentState
{
    /// <summary>The hooks are being read.</summary>
    Loading = 0,

    /// <summary>The hooks could not be read.</summary>
    Error = 1,

    /// <summary>The global Run hooks switch is off, so nothing runs.</summary>
    RunHooksOff = 2,

    /// <summary>The workspace is not trusted, so its project hooks are not loaded. Nothing else is listed.</summary>
    UntrustedWorkspace = 3,

    /// <summary>Every hook file that exists is invalid, so no hook is listed. The diagnostics say why.</summary>
    InvalidConfig = 4,

    /// <summary>There are no hook files. The page explains hooks and offers an example file.</summary>
    Empty = 5,

    /// <summary>Hooks are listed.</summary>
    Ready = 6,
}
