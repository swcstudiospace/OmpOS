namespace Autonogrammer.Presentation.Cloud;

/// <summary>Head hooks of <see cref="CloudAgentsViewModel"/>.</summary>
public sealed class CloudAgentsOptions
{
    /// <summary>Gets the hook that runs an action on the UI thread. <see cref="ICloudAgentsHost.Changes"/> may fire on any thread; the model hands its refresh to this hook. Null runs the action on the calling thread.</summary>
    public Action<Action>? Dispatch { get; init; }
}
