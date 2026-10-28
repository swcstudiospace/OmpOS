using System.Globalization;
using OmpOS.Presentation.Checkpoints;

namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>
/// Runs <c>/rewind [n]</c> (<see cref="HandlerActionIds.ChatRewind"/>), <c>/restore [n]</c> (<see cref="HandlerActionIds.ChatRestore"/>) and <c>/undo</c> (<see cref="HandlerActionIds.ChatUndo"/>) over the session's
/// <see cref="CheckpointsModel"/>. None of them rewinds by itself: each shows the timeline or the confirmation, and the files change only when the user confirms there. With no number the timeline opens; a number asks to
/// rewind to before that checkpoint; <c>/undo</c> asks for the latest one.
/// </summary>
public sealed class CheckpointSlashHandler : ISlashActionHandler
{
    private readonly Func<CheckpointsModel?> _model;
    private readonly Func<bool>? _isEnabled;

    /// <summary>Creates the handler of one action.</summary>
    /// <param name="actionId">One of <see cref="HandlerActionIds.ChatRewind"/>, <see cref="HandlerActionIds.ChatRestore"/> and <see cref="HandlerActionIds.ChatUndo"/>.</param>
    /// <param name="model">Returns the checkpoint model of the chat in front, or <see langword="null"/> when there is none.</param>
    /// <param name="isEnabled">Tells whether rewind is switched on (<see cref="DisplaySettings.RewindEnabled"/>), or <see langword="null"/> for always.</param>
    /// <exception cref="ArgumentException"><paramref name="actionId"/> is not one of the three.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public CheckpointSlashHandler(string actionId, Func<CheckpointsModel?> model, Func<bool>? isEnabled = null)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        ArgumentNullException.ThrowIfNull(model);
        if (actionId is not (HandlerActionIds.ChatRewind or HandlerActionIds.ChatRestore or HandlerActionIds.ChatUndo))
        {
            throw new ArgumentException("Not a checkpoint action: " + actionId, nameof(actionId));
        }

        ActionId = actionId;
        _model = model;
        _isEnabled = isEnabled;
    }

    /// <inheritdoc/>
    public string ActionId { get; }

    /// <inheritdoc/>
    public bool CanRun => _model() is not null;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_isEnabled is { } enabled && !enabled())
        {
            return SlashActionResult.NotAvailable(HandlerCopy.RewindOff);
        }

        var model = _model();
        if (model is null)
        {
            return SlashActionResult.NotAvailable(HandlerCopy.NotReady);
        }

        if (string.Equals(ActionId, HandlerActionIds.ChatUndo, StringComparison.Ordinal))
        {
            await model.RequestUndoLastAsync(cancellationToken).ConfigureAwait(true);
            return Result(model);
        }

        var typed = request.TypedText.Trim().TrimStart('#');
        if (typed.Length == 0)
        {
            await model.RefreshAsync(cancellationToken).ConfigureAwait(true);
            model.Open();
            return model.HasError ? SlashActionResult.Failed(model.StatusText) : SlashActionResult.Completed(HandlerCopy.CheckpointsOpened);
        }

        if (!int.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out var turn) || turn < 1)
        {
            return SlashActionResult.Failed(HandlerCopy.NeedsNumber);
        }

        await model.RequestRewindAsync(turn, cancellationToken).ConfigureAwait(true);
        return Result(model);
    }

    private static SlashActionResult Result(CheckpointsModel model) =>
        model.HasError ? SlashActionResult.Failed(model.StatusText) : SlashActionResult.Completed(model.StatusText);
}
