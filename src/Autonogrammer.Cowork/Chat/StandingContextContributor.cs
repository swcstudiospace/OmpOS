using Autonogrammer.Cowork.Instructions;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Cowork.Chat;

/// <summary>
/// Contributes the standing-context paragraph, the builder's first-prompt block (with untrusted fence and notices),
/// and memory.backend: off overlay for Cowork sessions. Code sessions receive the empty contribution.
/// The planner (75-06) applies without naming this type.
/// </summary>
public sealed class StandingContextContributor : ILaunchContributor
{
    /// <summary>
    /// Builds the contribution. For Cowork surface supplies the fixed paragraph (D-10), the builder block as first prompt resource,
    /// and the memory backend off overlay entry. Refuses paragraph over budget (defensive).
    /// </summary>
    public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Surface, "cowork", StringComparison.OrdinalIgnoreCase))
        {
            return EmptyContribution();
        }

        var paragraph = StandingContextBuilder.SystemPromptParagraph;
        var bytes = System.Text.Encoding.UTF8.GetByteCount(paragraph);
        if (bytes > 2 * 1024)
        {
            throw new ArgumentOutOfRangeException(paramName: null, message: $"Paragraph is {bytes} bytes, over 2 KiB budget.");
        }

        // For the seam the block can be empty here; the actual builder call with real data happens at session start
        // (see StandingContextContributorTests and the session host). Provide a minimal valid block for the seam test.
        var block = StandingContextBuilder.Build(null, null, null, null);

        return new LaunchContribution
        {
            Arguments = Array.Empty<string>(),
            Environment = new Dictionary<string, string?>(StringComparer.Ordinal),
            OverlayEntries = new[] { "memory:\n  backend: off\n" },
            SystemPromptParagraph = paragraph,
            FirstPromptBlock = block.Text,
            ProcessWrapper = null,
        };
    }

    private static LaunchContribution EmptyContribution() =>
        new()
        {
            Arguments = Array.Empty<string>(),
            Environment = new Dictionary<string, string?>(StringComparer.Ordinal),
            OverlayEntries = Array.Empty<string>(),
            SystemPromptParagraph = string.Empty,
            FirstPromptBlock = string.Empty,
            ProcessWrapper = null,
        };
}
