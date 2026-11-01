using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Contributes the session's isolation wrapper, or none, through <see cref="ILaunchContributor"/>.
/// It adds no arguments, environment or prompt text. The decision is built per session by the composition root.
/// </summary>
public sealed class IsolationContributor : ILaunchContributor
{
    private readonly IsolationDecision _decision;

    /// <summary>Initializes a new instance of the <see cref="IsolationContributor"/> class.</summary>
    /// <param name="decision">The decision for this session. A null wrapper means a direct launch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null.</exception>
    public IsolationContributor(IsolationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        _decision = decision;
    }

    /// <inheritdoc />
    public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return new LaunchContribution
        {
            Arguments = [],
            Environment = new Dictionary<string, string?>(StringComparer.Ordinal),
            OverlayEntries = [],
            SystemPromptParagraph = string.Empty,
            FirstPromptBlock = string.Empty,
            ProcessWrapper = _decision.Wrapper,
        };
    }
}
