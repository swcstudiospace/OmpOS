using Autonogrammer.Cowork.Instructions;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Cowork.Chat;

/// <summary>
/// Contributes, for Cowork sessions only, the fixed standing-context paragraph, the session's first-prompt block (the standing context with its untrusted
/// fence and notices) and the <c>memory.backend: off</c> overlay entry. A Code session receives the empty contribution. The planner applies this without
/// naming this type.
/// </summary>
public sealed class StandingContextContributor : ILaunchContributor
{
    /// <summary>The overlay entry that turns omp's own memory backend off for a Cowork session. Memory is the account's, not omp's.</summary>
    public const string MemoryOffOverlayEntry = "memory:\n  backend: off\n";

    private readonly string _firstPromptBlock;

    /// <summary>Initializes a new instance of the <see cref="StandingContextContributor"/> class.</summary>
    /// <param name="context">
    /// The standing context built from the account data the session took at its start, or null when there is none. Its text is the first-prompt block.
    /// </param>
    public StandingContextContributor(StandingContext? context = null)
    {
        _firstPromptBlock = context is { } built && !string.IsNullOrEmpty(built.Text) ? built.Text : string.Empty;
    }

    /// <summary>
    /// Builds the contribution. For the Cowork surface it supplies the fixed paragraph, the block the session took at its start, and the memory overlay entry.
    /// </summary>
    /// <param name="request">The launch being prepared.</param>
    /// <param name="cancellationToken">Unused: a contribution is pure data.</param>
    /// <returns>The contribution.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The fixed paragraph is over its budget. This is a defensive check.</exception>
    public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Surface, "cowork", StringComparison.OrdinalIgnoreCase))
        {
            return EmptyContribution();
        }

        var paragraph = StandingContextBuilder.SystemPromptParagraph;
        var bytes = System.Text.Encoding.UTF8.GetByteCount(paragraph);
        if (bytes > InstructionLimits.SystemPromptParagraphBytes)
        {
            throw new ArgumentOutOfRangeException(paramName: null, message: $"Paragraph is {bytes} bytes, over the {InstructionLimits.SystemPromptParagraphBytes} byte budget.");
        }

        return new LaunchContribution
        {
            Arguments = Array.Empty<string>(),
            Environment = new Dictionary<string, string?>(StringComparer.Ordinal),
            OverlayEntries = [MemoryOffOverlayEntry],
            SystemPromptParagraph = paragraph,
            FirstPromptBlock = _firstPromptBlock,
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
