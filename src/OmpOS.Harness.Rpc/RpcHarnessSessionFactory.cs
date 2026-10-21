using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Rpc;

/// <summary>
/// Creates omp RPC harness sessions. Each session runs real omp turns through <see cref="OmpRpcClient"/> and streams the
/// same <see cref="HarnessEvent"/> contract the worker already consumes.
/// </summary>
/// <remarks>
/// <para>
/// Contract decision: <see cref="IHarnessSessionFactory.CreateSession"/> is synchronous and the interface shape is
/// unchanged. Making it asynchronous would force edits in the API session-run activities, the in-memory factory and
/// five test implementers, none of which this adapter owns. The returned session therefore starts omp lazily, on its
/// first <see cref="IOmpSession.CreateAsync"/>. A start failure surfaces from that call and leaves the session
/// <see cref="HarnessState.Faulted"/>.
/// </para>
/// <para>
/// Pin behaviour: every start goes through <see cref="OmpRpcClient"/>, which probes <c>omp --version</c> and refuses
/// any release outside the 18.12.x line with a <see cref="ProcessStartHarnessException"/> before the engine is spawned.
/// No path in this factory skips that probe.
/// </para>
/// <para>
/// The <c>prompt</c> argument is accepted for interface parity. The session submits the prompt passed to
/// <see cref="IOmpSession.SubmitPromptAsync"/>, which is the text streamed to omp.
/// </para>
/// </remarks>
public sealed class RpcHarnessSessionFactory : IHarnessSessionFactory
{
    private readonly IRpcSessionStarter _starter;

    /// <summary>Initializes a new instance of the <see cref="RpcHarnessSessionFactory"/> class.</summary>
    /// <param name="client">The client that starts omp for every session. Not owned; not disposed.</param>
    public RpcHarnessSessionFactory(OmpRpcClient client)
        : this(new OmpRpcClientStarter(client))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RpcHarnessSessionFactory"/> class over an explicit starter.</summary>
    /// <param name="starter">Opens the engine sessions. Production code uses the <see cref="OmpRpcClient"/> constructor.</param>
    internal RpcHarnessSessionFactory(IRpcSessionStarter starter)
    {
        ArgumentNullException.ThrowIfNull(starter);
        _starter = starter;
    }

    /// <inheritdoc />
    /// <remarks>Spawns nothing. The engine starts on the returned session's <see cref="IOmpSession.CreateAsync"/>.</remarks>
    public IOmpSession CreateSession(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return new RpcHarnessSession(_starter);
    }
}
