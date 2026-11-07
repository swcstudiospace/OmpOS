using System.Net;

namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>Resolves names with <see cref="Dns.GetHostAddressesAsync(string, CancellationToken)"/> once per call.</summary>
public sealed class SystemHostResolver : IHostResolver
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        var answers = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        return answers;
    }
}
