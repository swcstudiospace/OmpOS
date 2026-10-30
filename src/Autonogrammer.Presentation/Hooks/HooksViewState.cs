namespace Autonogrammer.Presentation.Hooks;

/// <summary>The one state the hooks list is in. A head shows exactly one state at a time.</summary>
public enum HooksViewState
{
    /// <summary>The registry is loading.</summary>
    Loading,

    /// <summary>The registry loaded and holds no hooks.</summary>
    Empty,

    /// <summary>The registry failed to load. A head shows <see cref="HooksViewModel.LoadFailure"/>.</summary>
    Error,

    /// <summary>The registry loaded and every hook is ready to run.</summary>
    Success,

    /// <summary>The registry loaded and at least one hook is awaiting consent or belongs to a quarantined plugin.</summary>
    Degraded,
}
