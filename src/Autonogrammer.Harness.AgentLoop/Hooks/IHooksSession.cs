using System.Text.Json;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>
/// The hooks of one conversation: the loopback bridge omp is connected to, the gate the ACP session reports to, and the dispatcher that
/// runs the hooks behind both. Disposing it stops the bridge and finishes the observations it still holds.
/// </summary>
public interface IHooksSession : IAsyncDisposable
{
    /// <summary>Raised when a hook did something the thread must show: a denial, a missing consent, added context or a continuation.</summary>
    event EventHandler<HookNoticeEventArgs>? NoticeRaised;

    /// <summary>Gets the Autonogrammer session identifier the session was started for.</summary>
    string SessionId { get; }

    /// <summary>Gets a value indicating whether hooks are on for this session. False when the global <c>Run hooks</c> switch was off at the start.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the gate to put in <see cref="AcpClientOptions.HookGate"/>, or null when hooks are off for the session. It is the same instance for the
    /// life of the session.
    /// </summary>
    IAcpHookGate? Gate { get; }

    /// <summary>
    /// Gets the endpoint to give <see cref="OmpLaunchPlanner.Plan"/>, or null when hooks are off for the session or no bridge could be started.
    /// </summary>
    HookBridgeEndpoint? Endpoint { get; }

    /// <summary>Gets the hook set in force.</summary>
    EffectiveHookSet Hooks { get; }

    /// <summary>
    /// Reads the hook files again and swaps the new set in. A hook that changed needs consent again. The bridge keeps the event lists it started
    /// with, so a hook for an event that had none starts to fire with the next session.
    /// </summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the new set is in force.</returns>
    Task ReloadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs the hooks of an event the head itself produces, such as <c>workspaceOpen</c> or a manual <c>preCompact</c>. With hooks off it returns
    /// <see cref="HookDecision.None"/>.
    /// </summary>
    /// <param name="hookEvent">The event.</param>
    /// <param name="payload">The event-specific payload (see <see cref="HookPayloads"/>).</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>The merged decision.</returns>
    Task<HookDecision> DispatchAsync(HookEvent hookEvent, JsonElement payload, CancellationToken cancellationToken);

    /// <summary>Returns the newest run records of this session, oldest first.</summary>
    /// <param name="max">The most records to return.</param>
    /// <returns>The records.</returns>
    IReadOnlyList<HookRunRecord> RecentRuns(int max);
}
