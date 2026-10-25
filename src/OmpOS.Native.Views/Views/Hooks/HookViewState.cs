namespace OmpOS.Native.Views.Hooks;

/// <summary>The one state the hooks view is in. The view shows exactly one state at a time.</summary>
internal enum HookViewState
{
    /// <summary>The registry is loading.</summary>
    Loading,

    /// <summary>The registry loaded and holds no hooks.</summary>
    Empty,

    /// <summary>The registry failed to load. The view shows the reason.</summary>
    Error,

    /// <summary>The registry loaded and every hook is ready to run after consent.</summary>
    Success,

    /// <summary>The registry loaded and at least one hook is awaiting consent or belongs to a quarantined plugin.</summary>
    Degraded,
}
