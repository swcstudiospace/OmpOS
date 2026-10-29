using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Abstractions;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// Carries the ACP stdio protocol of <c>omp acp</c> running inside a podman container, through <c>podman exec -i</c>. It is the ssh transport with podman as the
/// client: one JSON frame per line in each direction, text before the first frame tolerated as noise, the preamble (environment and files) written before the
/// first frame, secrets removed from every diagnostic text. A failed run is explained by <see cref="LastDiagnosis"/> (container missing, not running, user
/// missing, omp missing). The process is started without a shell from an argument list and no secret is ever in it.
/// </summary>
public sealed class PodmanAcpTransport : IAcpTransport
{
    private readonly SshAcpTransport _inner;

    /// <summary>Initializes a new instance of the <see cref="PodmanAcpTransport"/> class.</summary>
    /// <param name="invocation">The podman process to start, from <see cref="PodmanExecCommandBuilder.BuildAcp"/>.</param>
    /// <param name="redactions">Secret values that are removed from every diagnostic text. Values shorter than four characters are ignored.</param>
    /// <param name="logger">Receives stderr drain failures at Debug. Null discards them.</param>
    public PodmanAcpTransport(SshInvocation invocation, IEnumerable<string>? redactions = null, ILogger<SshAcpTransport>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        _inner = new SshAcpTransport(invocation, ExecTransportFlavor.Podman, redactions, logger);
    }

    /// <summary>Gets the most recent stderr lines of the podman process (at most <see cref="SshAcpTransport.StderrCapacity"/>), with secrets removed.</summary>
    public IReadOnlyList<string> StandardErrorTail => _inner.StandardErrorTail;

    /// <summary>Gets the lines the shell printed on stdout before the first protocol frame, with secrets removed.</summary>
    public IReadOnlyList<string> PreHandshakeNoise => _inner.PreHandshakeNoise;

    /// <summary>Gets the explanation of the failure that ended the connection, or <see langword="null"/> while it is up or when the cause is not recognised.</summary>
    public RemoteDiagnosis? LastDiagnosis => _inner.LastDiagnosis;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken) => _inner.ConnectAsync(cancellationToken);

    /// <inheritdoc />
    public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken) => _inner.SendAsync(frame, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) => _inner.ReceiveAsync(cancellationToken);

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken) => _inner.CloseAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
