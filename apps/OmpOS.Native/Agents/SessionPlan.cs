using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Hooks;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Substrate;
using OmpOS.Native.Composition;

namespace OmpOS.Native.Agents;

/// <summary>
/// Everything one session needs before its engine can start, decided once: the launch plan, the merged stage source, the task
/// context source, the steering recorder (only when the Substrate answered its health check), the Substrate gateway used for a
/// re-check, and the HTTP client behind the gateway. The plan owns that client and disposes it.
/// </summary>
internal sealed class SessionPlan : IDisposable, IAsyncDisposable
{
    private readonly HttpClient? _substrateClient;

    /// <summary>Creates the plan.</summary>
    /// <param name="launch">How omp is launched for the session.</param>
    /// <param name="stages">The merged stage source of the session.</param>
    /// <param name="context">The task context source of the session.</param>
    /// <param name="steering">Records steering in the Substrate ledger, or null when the Substrate is not active for the session.</param>
    /// <param name="recheck">The source that carries the events a re-check publishes.</param>
    /// <param name="gateway">The Substrate gateway, or null when no Substrate is configured.</param>
    /// <param name="substrateActive">Whether the Substrate answered its health check and is used by this session.</param>
    /// <param name="substrateClient">The HTTP client behind <paramref name="gateway"/>. The plan disposes it.</param>
    public SessionPlan(
        OmpLaunchPlan launch,
        IStageSource stages,
        ITaskContextSource context,
        SteeringRecorderAdapter? steering,
        RecheckStageSource recheck,
        SubstrateGateway? gateway,
        bool substrateActive,
        HttpClient? substrateClient)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(recheck);
        Launch = launch;
        Stages = stages;
        Context = context;
        Steering = steering;
        Recheck = recheck;
        Gateway = gateway;
        SubstrateActive = substrateActive;
        _substrateClient = substrateClient;
    }

    /// <summary>Gets how omp is launched for the session.</summary>
    public OmpLaunchPlan Launch { get; }

    /// <summary>Gets the merged stage source of the session.</summary>
    public IStageSource Stages { get; }

    /// <summary>Gets the task context source of the session.</summary>
    public ITaskContextSource Context { get; }

    /// <summary>Gets the steering recorder adapter, or null when the Substrate is not active for the session.</summary>
    public SteeringRecorderAdapter? Steering { get; }

    /// <summary>Gets the source that carries the events a re-check publishes.</summary>
    public RecheckStageSource Recheck { get; }

    /// <summary>Gets the Substrate gateway, or null when no Substrate is configured.</summary>
    public SubstrateGateway? Gateway { get; }

    /// <summary>Gets a value indicating whether the Substrate answered its health check and is used by this session.</summary>
    public bool SubstrateActive { get; }

    /// <summary>Gets the hooks of the session, or null when hooks are off for it (no hooks service, the global switch off, or the start failed). The session's engine reports to its gate; it is disposed after the engine.</summary>
    public IHooksSession? Hooks { get; init; }

    /// <summary>Gets the run mode policy of the session's workspace, or null for a session without run modes.</summary>
    public RunModePolicyHolder? RunModes { get; init; }

    /// <summary>Gets the writer that owns the settings overlay file omp reads (<c>--config</c>), or null when the plan needs none. Disposing it deletes the file.</summary>
    public ConfigOverlayWriter? Overlay { get; init; }

    /// <summary>Gets the identifier of the session the plan is for.</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>Gets what the head remembers about the session: the ssh environment it runs on and the other roots of its workspace.</summary>
    public SessionExtras Extras { get; init; } = SessionExtras.Local;

    /// <summary>Releases what the plan owns without waiting: the HTTP client of the Substrate gateway, the overlay file and the run mode holder. The hooks of the session are released by <see cref="DisposeAsync"/>.</summary>
    public void Dispose()
    {
        _substrateClient?.Dispose();
        Overlay?.Dispose();
        RunModes?.Dispose();
    }

    /// <summary>Releases everything the plan owns, and ends the hooks of the session (its disposal sends <c>sessionEnd</c>) after the engine that reported to them has stopped.</summary>
    /// <returns>A task that completes when everything is released.</returns>
    public async ValueTask DisposeAsync()
    {
        Dispose();
        if (Hooks is { } hooks)
        {
            await hooks.DisposeAsync().ConfigureAwait(false);
        }
    }
}
