using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Rpc;

/// <summary>Opens sessions through <see cref="OmpRpcClient"/>, which enforces the omp version pin on every start.</summary>
internal sealed class OmpRpcClientStarter : IRpcSessionStarter
{
    private readonly OmpRpcClient _client;

    /// <summary>Initializes a new instance of the <see cref="OmpRpcClientStarter"/> class.</summary>
    /// <param name="client">The client whose sessions this starter opens. Not owned; not disposed.</param>
    public OmpRpcClientStarter(OmpRpcClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    public Task<IOmpSession> CreateAsync(CancellationToken cancellationToken) =>
        _client.CreateSessionAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IOmpSession> ResumeAsync(string sessionPath, CancellationToken cancellationToken) =>
        _client.ResumeSessionAsync(sessionPath, cancellationToken);
}
