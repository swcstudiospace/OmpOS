namespace OmpOS.Presentation.Commands;

/// <summary>
/// The head's side of <see cref="SlashHandlerKind.NativeAction"/> commands. The desktop head implements it in <c>OmpOS.Native.Views</c>; the web head implements it in a later wave.
/// A head that cannot run an action says so through <see cref="CanRun"/>, and the palette then shows the command as unavailable instead of listing a control that does nothing.
/// </summary>
public interface ISlashActionHost
{
    /// <summary>Tells whether this head can run an action now.</summary>
    /// <param name="actionId">One of <see cref="SlashActionIds"/>.</param>
    /// <returns><see langword="true"/> when <see cref="RunAsync"/> would run it.</returns>
    bool CanRun(string actionId);

    /// <summary>Runs an action.</summary>
    /// <param name="request">The action and its arguments.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>How the action ended.</returns>
    Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken);
}
