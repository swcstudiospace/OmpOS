// Owners 75-06 and 76-04 extend these Harness.Abstractions seam files; never recreate them.
// Any signature detail beyond the brief names (75-cowork-parity-a.md:49, 76-cowork-parity-b.md:47)
// is inference recorded here: LaunchRequest carries surface, session id, working directory and
// trusted folders; ILaunchContributor.Contribute is synchronous because a contribution is pure data.
namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// Contributes launch arguments, environment, overlays and prompt text for a session launch.
/// Owners 75-06 and 76-04 add contributors; this interface itself stays stable.
/// </summary>
public interface ILaunchContributor
{
    /// <summary>Builds the contribution for the given launch request.</summary>
    /// <param name="request">The session launch being prepared.</param>
    /// <param name="cancellationToken">Cancels the contribution.</param>
    /// <returns>The contribution to apply to the launch.</returns>
    LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken);
}
