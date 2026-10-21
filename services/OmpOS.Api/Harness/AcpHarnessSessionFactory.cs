using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;

namespace OmpOS.Api.Harness;

/// <summary>
/// <see cref="IHarnessSessionFactory"/> backed by <see cref="OmpAcpClient"/>. Each prompt opens an <c>omp acp</c> session over
/// stdio, started from <c>Harness:Acp:OmpPath</c>. The client holds the permission resolver, so every permission request
/// reaches the ACP agent only through that resolver.
/// </summary>
internal sealed class AcpHarnessSessionFactory : IHarnessSessionFactory
{
    private readonly OmpAcpClient _client;
    private readonly IOptions<HarnessOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="AcpHarnessSessionFactory"/> class.</summary>
    /// <param name="client">The ACP client that starts omp.</param>
    /// <param name="options">The harness options naming the omp executable.</param>
    public AcpHarnessSessionFactory(OmpAcpClient client, IOptions<HarnessOptions> options)
    {
        _client = client;
        _options = options;
    }

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return _client.CreateSession(new AcpClientOptions { OmpPath = _options.Value.Acp.OmpPath });
    }
}
