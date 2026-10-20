using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Acp;

/// <summary>
/// Composition root for the ACP v1 client. Creates sessions that speak to <c>omp acp</c>, or to any transport the
/// caller supplies.
/// </summary>
public sealed class OmpAcpClient
{
    private const string DefaultExecutable = "omp";

    private readonly Func<AcpClientOptions, IAcpTransport> _transportFactory;

    /// <summary>Initializes a new instance of the <see cref="OmpAcpClient"/> class that spawns <c>omp acp</c>.</summary>
    public OmpAcpClient()
        : this(static options => new AcpStdioTransport(options.OmpPath ?? DefaultExecutable, options.WorkingDirectory))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmpAcpClient"/> class with a custom transport factory.</summary>
    /// <param name="transportFactory">Creates the transport for each session.</param>
    public OmpAcpClient(Func<AcpClientOptions, IAcpTransport> transportFactory)
    {
        ArgumentNullException.ThrowIfNull(transportFactory);
        _transportFactory = transportFactory;
    }

    /// <summary>Creates a session. The session is not connected until <see cref="IOmpSession.CreateAsync"/> or <see cref="IOmpSession.ResumeAsync"/> runs.</summary>
    /// <param name="options">Session options.</param>
    /// <returns>An unconnected session.</returns>
    public IOmpSession CreateSession(AcpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AcpAgentSession(_transportFactory(options), options);
    }
}
