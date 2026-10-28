using System.Globalization;
using OmpOS.Presentation.Worktrees;

namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>
/// Runs the worktree commands over the repository's <see cref="WorktreeCommandsModel"/>: <see cref="HandlerActionIds.WorktreeStart"/> (a name), <see cref="HandlerActionIds.WorktreeApply"/> (a run and the word <c>merge</c> for a three-way merge),
/// <see cref="HandlerActionIds.WorktreeDelete"/> (a run; it only asks, the deletion is confirmed in the Worktrees panel) and <see cref="HandlerActionIds.WorktreeBestOfN"/> (<c>n task</c>).
/// </summary>
public sealed class WorktreeSlashHandler : ISlashActionHandler
{
    private readonly Func<WorktreeCommandsModel?> _model;

    /// <summary>Creates the handler of one action.</summary>
    /// <param name="actionId">One of the four worktree action ids.</param>
    /// <param name="model">Returns the worktree model of the repository in front, or <see langword="null"/> when there is none.</param>
    /// <exception cref="ArgumentException"><paramref name="actionId"/> is not a worktree action.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public WorktreeSlashHandler(string actionId, Func<WorktreeCommandsModel?> model)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        ArgumentNullException.ThrowIfNull(model);
        if (actionId is not (HandlerActionIds.WorktreeStart or HandlerActionIds.WorktreeApply or HandlerActionIds.WorktreeDelete or HandlerActionIds.WorktreeBestOfN))
        {
            throw new ArgumentException("Not a worktree action: " + actionId, nameof(actionId));
        }

        ActionId = actionId;
        _model = model;
    }

    /// <inheritdoc/>
    public string ActionId { get; }

    /// <inheritdoc/>
    public bool CanRun => _model() is not null;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var model = _model();
        if (model is null)
        {
            return SlashActionResult.NotAvailable(HandlerCopy.NotReady);
        }

        var typed = request.TypedText.Trim();
        switch (ActionId)
        {
            case HandlerActionIds.WorktreeStart:
                await model.StartAsync(typed, cancellationToken).ConfigureAwait(true);
                return Result(model);
            case HandlerActionIds.WorktreeApply:
                var words = typed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var merge = words.Length > 0 && string.Equals(words[^1], "merge", StringComparison.OrdinalIgnoreCase);
                var run = merge ? string.Join(' ', words[..^1]) : string.Join(' ', words);
                var outcome = await model.ApplyAsync(run, merge ? WorktreeApplyMethod.ThreeWay : WorktreeApplyMethod.Clean, cancellationToken).ConfigureAwait(true);
                return outcome is { State: WorktreeApplyState.Applied or WorktreeApplyState.NothingToApply } ? SlashActionResult.Completed(model.StatusText) : SlashActionResult.Failed(model.StatusText);
            case HandlerActionIds.WorktreeDelete:
                await model.RequestDeleteAsync(typed, cancellationToken).ConfigureAwait(true);
                return model.IsConfirmingDelete ? SlashActionResult.Completed(model.StatusText) : SlashActionResult.Failed(model.StatusText);
            default:
                return await BestOfNAsync(model, typed, cancellationToken).ConfigureAwait(true);
        }
    }

    private static async Task<SlashActionResult> BestOfNAsync(WorktreeCommandsModel model, string typed, CancellationToken cancellationToken)
    {
        var split = typed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length == 0 || !int.TryParse(split[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            return SlashActionResult.Failed(HandlerCopy.BestOfNeedsCount);
        }

        await model.BestOfNAsync(split.Length > 1 ? split[1] : string.Empty, count, cancellationToken).ConfigureAwait(true);
        return Result(model);
    }

    private static SlashActionResult Result(WorktreeCommandsModel model) =>
        model.HasError ? SlashActionResult.Failed(model.StatusText) : SlashActionResult.Completed(model.StatusText);
}
