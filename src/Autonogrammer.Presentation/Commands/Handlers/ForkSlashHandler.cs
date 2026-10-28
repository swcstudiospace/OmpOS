using System.Globalization;
using Autonogrammer.Presentation.SideChats;

namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>Runs <c>/fork</c>, <c>/branch</c> and <c>/duplicate</c> (<see cref="HandlerActionIds.ChatFork"/>) over the session's <see cref="ForkModel"/>. With no number the whole chat is forked; with a number, the chat up to that message.</summary>
public sealed class ForkSlashHandler : ISlashActionHandler
{
    private readonly Func<ForkModel?> _model;

    /// <summary>Creates the handler.</summary>
    /// <param name="model">Returns the fork model of the chat in front, or <see langword="null"/> when there is none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public ForkSlashHandler(Func<ForkModel?> model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    /// <inheritdoc/>
    public string ActionId => HandlerActionIds.ChatFork;

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
        if (typed.Length == 0)
        {
            await model.DuplicateAsync(cancellationToken).ConfigureAwait(true);
        }
        else if (int.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            await model.ForkFromMessageAsync(count, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            return SlashActionResult.Failed("Type the number of messages to keep, for example /fork 4, or nothing to fork the whole chat.");
        }

        return model.HasError ? SlashActionResult.Failed(model.StatusText) : SlashActionResult.Completed(model.StatusText);
    }
}
