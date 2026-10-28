using Autonogrammer.Contracts.Ui;

namespace Autonogrammer.GenUi;

/// <summary>
/// The tool-result, file-edit and approval-decision rules of <see cref="UiFolder"/>, kept apart from the main fold.
/// </summary>
/// <remarks>
/// These rules are equal to the ones in <c>HarnessEventProjection</c>: the same block identifiers
/// (<c>{callId}-result</c> and <c>{callId}-edit-{n}</c>), the same replace-in-place semantics, and the same
/// permission addressing. The update content itself is read by <see cref="ToolUpdateContent.Read"/>, which both
/// folds share through the contracts. A test replays the same recorded events through both folds and compares the
/// blocks.
/// </remarks>
internal static class UiToolBlocks
{
    /// <summary>
    /// Adds the result and file-edit blocks a tool-call update carries after the transcript's blocks. A block this
    /// call already produced (same identifier) is replaced in place, because an update's content stands for the whole
    /// content, so a later update never appends a second copy. An update with no output and no edits changes nothing.
    /// </summary>
    /// <param name="state">The tree after the call card took the update's status.</param>
    /// <param name="call">The call card the update addresses, before the status change.</param>
    /// <param name="status">The status the update reported.</param>
    /// <param name="content">The output and edits the update carried.</param>
    /// <returns>The tree with the produced blocks added or replaced.</returns>
    internal static UiTree ApplyToolContent(UiTree state, ToolCallBlock call, string status, ToolUpdateContent content)
    {
        var produced = new List<UiBlock>();
        if (content.Output.Length > 0)
        {
            produced.Add(new ToolResultBlock
            {
                Id = $"{call.CallId}-result",
                CallId = call.CallId,
                ToolName = call.ToolName,
                Status = status,
                Output = content.Output,
            });
        }

        for (var i = 0; i < content.Edits.Count; i++)
        {
            var edit = content.Edits[i];
            produced.Add(new FileEditBlock
            {
                Id = $"{call.CallId}-edit-{i}",
                CallId = call.CallId,
                Path = edit.Path,
                OldText = edit.OldText,
                NewText = edit.NewText,
            });
        }

        var current = state;
        foreach (var block in produced)
        {
            var index = FindBlock(current.Blocks, block.Id);
            if (index >= 0)
            {
                var replaced = new List<UiBlock>(current.Blocks) { [index] = block };
                current = current with { Blocks = replaced };
            }
            else
            {
                current = UiFolder.Append(UiFolder.Flush(current), block);
            }
        }

        return current;
    }

    /// <summary>
    /// Finds the latest permission block with no decision that the request identifier addresses: either its own
    /// identifier, or a run-scoped identifier of the form <c>{runId}:{blockId}</c>.
    /// </summary>
    /// <param name="blocks">The blocks to search.</param>
    /// <param name="requestId">The request identifier the decision event carries.</param>
    /// <returns>The block index, or -1 when no undecided permission matches.</returns>
    internal static int FindUndecidedPermission(IReadOnlyList<UiBlock> blocks, string requestId)
    {
        for (var i = blocks.Count - 1; i >= 0; i--)
        {
            if (blocks[i] is PermissionBlock { Decision: null } permission
                && (string.Equals(permission.Id, requestId, StringComparison.Ordinal)
                    || requestId.EndsWith(':' + permission.Id, StringComparison.Ordinal)))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Records a decision on the permission block at an index found by <see cref="FindUndecidedPermission"/>.</summary>
    /// <param name="state">The tree before the decision.</param>
    /// <param name="index">The index of the undecided permission block.</param>
    /// <param name="decision">The decision, a value of <see cref="ApprovalDecisions"/>.</param>
    /// <returns>The tree with the decision set.</returns>
    internal static UiTree Decide(UiTree state, int index, string decision)
    {
        var decided = new List<UiBlock>(state.Blocks);
        decided[index] = ((PermissionBlock)state.Blocks[index]) with { Decision = decision };
        return state with { Blocks = decided };
    }

    private static int FindBlock(IReadOnlyList<UiBlock> blocks, string id)
    {
        for (var i = blocks.Count - 1; i >= 0; i--)
        {
            if (string.Equals(blocks[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
