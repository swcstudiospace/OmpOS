using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Acp;

/// <summary>
/// Composition root for the ACP v1 client. Creates sessions that speak to <c>omp acp</c>, or to any transport the
/// caller supplies. Every session it creates shares its permission resolver: a permission request the approval policy
/// admits waits for the resolver's decision, and a request with no resolver is denied.
/// </summary>
public sealed class OmpAcpClient
{
    private const string DefaultExecutable = "omp";

    private readonly Func<AcpClientOptions, IAcpTransport> _transportFactory;
    private readonly IPermissionResolver? _permissionResolver;
    private readonly TimeSpan _permissionDecisionTimeout;

    /// <summary>Initializes a new instance of the <see cref="OmpAcpClient"/> class that spawns <c>omp acp</c>. With no resolver, every permission request is denied.</summary>
    public OmpAcpClient()
        : this(SpawnOmpAcp, permissionResolver: null, permissionDecisionTimeout: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmpAcpClient"/> class that spawns <c>omp acp</c>.</summary>
    /// <param name="permissionResolver">
    /// Decides the permission requests the approval policy admits. Null denies every such request.
    /// </param>
    /// <param name="permissionDecisionTimeout">
    /// How long an admitted request waits for its decision before it is denied. Null uses the session default.
    /// </param>
    public OmpAcpClient(IPermissionResolver? permissionResolver, TimeSpan? permissionDecisionTimeout = null)
        : this(SpawnOmpAcp, permissionResolver, permissionDecisionTimeout)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmpAcpClient"/> class with a custom transport factory and no resolver.</summary>
    /// <param name="transportFactory">Creates the transport for each session.</param>
    public OmpAcpClient(Func<AcpClientOptions, IAcpTransport> transportFactory)
        : this(transportFactory, permissionResolver: null, permissionDecisionTimeout: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmpAcpClient"/> class with a custom transport factory.</summary>
    /// <param name="transportFactory">Creates the transport for each session.</param>
    /// <param name="permissionResolver">
    /// Decides the permission requests the approval policy admits. Null denies every such request.
    /// </param>
    /// <param name="permissionDecisionTimeout">
    /// How long an admitted request waits for its decision before it is denied. Null uses the session default.
    /// </param>
    public OmpAcpClient(Func<AcpClientOptions, IAcpTransport> transportFactory, IPermissionResolver? permissionResolver, TimeSpan? permissionDecisionTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(transportFactory);
        if (permissionDecisionTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(permissionDecisionTimeout), permissionDecisionTimeout, "The permission decision timeout must be positive.");
        }

        _transportFactory = transportFactory;
        _permissionResolver = permissionResolver;
        _permissionDecisionTimeout = permissionDecisionTimeout ?? AcpAgentSession.DefaultPermissionDecisionTimeout;
    }

    /// <summary>Creates a session. The session is not connected until <see cref="IOmpSession.CreateAsync"/> or <see cref="IOmpSession.ResumeAsync"/> runs.</summary>
    /// <param name="options">Session options.</param>
    /// <returns>An unconnected session.</returns>
    public IOmpSession CreateSession(AcpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AcpAgentSession(_transportFactory(options), options, _permissionResolver, _permissionDecisionTimeout);
    }

    private static IAcpTransport SpawnOmpAcp(AcpClientOptions options) =>
        new AcpStdioTransport(options.OmpPath ?? DefaultExecutable, options.WorkingDirectory);
}
