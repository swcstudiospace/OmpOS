using System.Net;
namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>
/// Resolves a host to the full answer set used for both the allow decision and the pinned dial.
/// The caller must not look the name up again.
/// </summary>
public interface IHostResolver
{
    /// <summary>Resolves <paramref name="host"/> to every address in the answer set.</summary>
    /// <param name="host">The host name. Literals are not passed here.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The full answer set.</returns>
    ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}
