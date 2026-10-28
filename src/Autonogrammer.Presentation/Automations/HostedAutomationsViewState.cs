namespace Autonogrammer.Presentation.Automations;

/// <summary>The one state the hosted automations page is in. A head shows exactly one state at a time.</summary>
public enum HostedAutomationsViewState
{
    /// <summary>The automations are loading.</summary>
    Loading,

    /// <summary>The service has automations switched off.</summary>
    Unavailable,

    /// <summary>The automations loaded and none exists.</summary>
    Empty,

    /// <summary>The automations failed to load. A head shows <see cref="HostedAutomationsViewModel.LoadFailure"/>.</summary>
    Error,

    /// <summary>The automations loaded and at least one exists.</summary>
    Ready,
}
