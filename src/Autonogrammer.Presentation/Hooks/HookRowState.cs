namespace Autonogrammer.Presentation.Hooks;

/// <summary>The one state a hook row shows. When several apply, the first of this list wins.</summary>
public enum HookRowState
{
    /// <summary>Autonogrammer cannot produce the event of the hook (Cursor Tab), so it never runs.</summary>
    NotApplicable = 0,

    /// <summary>The event name is not one Autonogrammer knows, so the hook never runs.</summary>
    UnknownEvent = 1,

    /// <summary>The source of the hook is quarantined and its guarding hooks block.</summary>
    Quarantined = 2,

    /// <summary>The hook is switched off, in its file or by you.</summary>
    Disabled = 3,

    /// <summary>The hook has no consent for its exact command, so it does not run.</summary>
    NeedsConsent = 4,

    /// <summary>The hook is running now.</summary>
    Running = 5,

    /// <summary>The hook's last run failed, timed out, was refused or gave an unusable answer.</summary>
    LastRunFailed = 6,

    /// <summary>The hook is enabled and consented to and its last run, if any, went well.</summary>
    Ready = 7,
}
