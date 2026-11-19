using Autonogrammer.Contracts.Ui;

namespace Autonogrammer.Presentation.Changes;

/// <summary>
/// The tool calls whose edits never reached the file. A call counts as failed when its last reported status is failed or cancelled, so a
/// later status that is neither makes it count again. The session's change set and the per-turn change sets both read failures here.
/// </summary>
internal static class FailedToolCalls
{
    private static readonly string[] FailedStatuses = ["failed", "cancelled", "canceled"];

    /// <summary>Gets the ids of the tool calls that failed in a transcript.</summary>
    /// <param name="blocks">The transcript's blocks, in order.</param>
    /// <returns>The call ids whose last status is a failure.</returns>
    public static HashSet<string> Of(IEnumerable<UiBlock> blocks)
    {
        var failed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in blocks.OfType<ToolCallBlock>())
        {
            if (FailedStatuses.Contains(call.Status, StringComparer.OrdinalIgnoreCase))
            {
                failed.Add(call.CallId);
            }
            else
            {
                failed.Remove(call.CallId);
            }
        }

        return failed;
    }
}
