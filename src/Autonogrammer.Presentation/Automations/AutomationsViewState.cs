namespace Autonogrammer.Presentation.Automations;

/// <summary>The one state the automations list is in. A head shows exactly one state at a time.</summary>
public enum AutomationsViewState
{
    /// <summary>The automations are loading.</summary>
    Loading,

    /// <summary>The automations loaded and none is registered.</summary>
    Empty,

    /// <summary>The automations failed to load. A head shows <see cref="AutomationsViewModel.LoadFailure"/>.</summary>
    Error,

    /// <summary>The automations loaded and none is paused.</summary>
    Success,

    /// <summary>The automations loaded and at least one is paused by a guard, so it refuses every run until it is resumed.</summary>
    Degraded,
}
