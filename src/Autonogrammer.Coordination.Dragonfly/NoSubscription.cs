namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>The handle of a subscribe that Dragonfly could not answer. It delivers nothing and disposing it does nothing.</summary>
internal sealed class NoSubscription : IAsyncDisposable
{
    /// <summary>Gets the shared instance.</summary>
    public static NoSubscription Instance { get; } = new();

    /// <summary>Does nothing.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
