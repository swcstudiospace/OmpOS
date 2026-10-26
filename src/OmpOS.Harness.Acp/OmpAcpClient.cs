using Microsoft.Extensions.Logging;
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
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>Initializes a new instance of the <see cref="OmpAcpClient"/> class that spawns <c>omp acp</c>. With no resolver, every permission request is denied.</summary>
    public OmpAcpClient()
        : this(SpawnOmpAcp(loggerFactory: null), permissionResolver: null, permissionDecisionTimeout: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmpAcpClient"/> class that spawns <c>omp acp</c>.</summary>
    /// <param name="permissionResolver">
    /// Decides the permission requests the approval policy admits. Null denies every such request.
    /// </param>
    /// <param name="permissionDecisionTimeout">
    /// How long an admitted request waits for its decision before it is denied. Null uses the session default.
    /// </param>
    /// <param name="loggerFactory">
    /// Creates the loggers of the sessions and of the stdio transports this client creates. Null discards their log output.
    /// </param>
    public OmpAcpClient(IPermissionResolver? permissionResolver, TimeSpan? permissionDecisionTimeout = null, ILoggerFactory? loggerFactory = null)
        : this(SpawnOmpAcp(loggerFactory), permissionResolver, permissionDecisionTimeout, loggerFactory)
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
    /// <param name="loggerFactory">Creates the logger of each session. Null discards the sessions' log output.</param>
    public OmpAcpClient(Func<AcpClientOptions, IAcpTransport> transportFactory, IPermissionResolver? permissionResolver, TimeSpan? permissionDecisionTimeout = null, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(transportFactory);
        if (permissionDecisionTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(permissionDecisionTimeout), permissionDecisionTimeout, "The permission decision timeout must be positive.");
        }

        _transportFactory = transportFactory;
        _permissionResolver = permissionResolver;
        _permissionDecisionTimeout = permissionDecisionTimeout ?? AcpAgentSession.DefaultPermissionDecisionTimeout;
        _loggerFactory = loggerFactory;
    }

    /// <summary>Creates a session. The session is not connected until <see cref="IOmpSession.CreateAsync"/> or <see cref="IOmpSession.ResumeAsync"/> runs.</summary>
    /// <param name="options">Session options.</param>
    /// <returns>An unconnected session.</returns>
    public IOmpSession CreateSession(AcpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AcpAgentSession(_transportFactory(options), options, _permissionResolver, _permissionDecisionTimeout, _loggerFactory?.CreateLogger<AcpAgentSession>());
    }

    private static Func<AcpClientOptions, IAcpTransport> SpawnOmpAcp(ILoggerFactory? loggerFactory) =>
        options => new AcpStdioTransport(
            options.OmpPath ?? DefaultExecutable,
            options.WorkingDirectory,
            options.ExtraArguments,
            options.EnvironmentVariables,
            loggerFactory?.CreateLogger<AcpStdioTransport>());
}
