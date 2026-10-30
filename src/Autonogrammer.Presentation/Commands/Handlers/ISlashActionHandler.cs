namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>One native action of the slash palette, run by a presentation model or a host seam. <see cref="SlashActionHandlerHost"/> routes <see cref="SlashActionRequest"/>s to handlers by <see cref="ActionId"/>.</summary>
public interface ISlashActionHandler
{
    /// <summary>Gets the action this handler runs, one of <see cref="HandlerActionIds"/> or <see cref="SlashActionIds"/>.</summary>
    string ActionId { get; }

    /// <summary>Gets whether the handler can run now. It is <see langword="false"/> while the thing it works on is missing, for example no open session.</summary>
    bool CanRun { get; }

    /// <summary>Runs the action. An expected problem is a <see cref="SlashActionResult"/>, not an exception.</summary>
    /// <param name="request">The action, its fixed argument and the text the user typed after the command.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>How the action ended.</returns>
    Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken);
}
