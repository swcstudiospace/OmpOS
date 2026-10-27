using OmpOS.Presentation.Commands;

namespace OmpOS.Native.Views.Sessions;

/// <summary>
/// The desktop head's <see cref="ISlashActionHost"/>: it maps each action id of <see cref="SlashActionIds"/> to the matching callback of <see cref="DesktopSlashTargets"/>, reads the action's
/// argument, and turns the outcome into a sentence for the composer's status line. An action whose callback is null cannot run (<see cref="CanRun"/> is false), and the cloud hand-off cannot run until the cloud phase.
/// </summary>
public sealed class DesktopSlashActionHost : ISlashActionHost
{
    /// <summary>The sentence shown for the cloud hand-off while cloud agents are not built.</summary>
    public const string CloudNotBuilt = "cloud agents: phase 60";

    private readonly DesktopSlashTargets _targets;

    /// <summary>Initializes a new instance of the <see cref="DesktopSlashActionHost"/> class.</summary>
    /// <param name="targets">The callbacks the actions drive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="targets"/> is null.</exception>
    public DesktopSlashActionHost(DesktopSlashTargets targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        _targets = targets;
    }

    /// <inheritdoc/>
    public bool CanRun(string actionId)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        return actionId switch
        {
            SlashActionIds.SettingsOpen => _targets.OpenSettings is not null,
            SlashActionIds.ChatNew => _targets.NewChat is not null,
            SlashActionIds.TerminalNew => _targets.NewTerminal is not null,
            SlashActionIds.ModeSet => _targets.SetMode is not null,
            SlashActionIds.ModelPick => _targets.PickModel is not null,
            SlashActionIds.SideOpen => _targets.OpenSideChat is not null,
            SlashActionIds.HelpShow => _targets.ShowHelp is not null,
            _ => false,
        };
    }

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!CanRun(request.ActionId))
        {
            return SlashActionResult.NotAvailable(
                string.Equals(request.ActionId, SlashActionIds.CloudHandoff, StringComparison.Ordinal) ? CloudNotBuilt : SlashPaletteCopy.ActionNotInThisWindow);
        }

        try
        {
            return request.ActionId switch
            {
                SlashActionIds.SettingsOpen => OpenSettings(request),
                SlashActionIds.ChatNew => await RunTargetAsync(_targets.NewChat!, "Started a new chat.", cancellationToken).ConfigureAwait(true),
                SlashActionIds.TerminalNew => await RunTargetAsync(_targets.NewTerminal!, "Started a new terminal.", cancellationToken).ConfigureAwait(true),
                SlashActionIds.ModeSet => await SetModeAsync(request, cancellationToken).ConfigureAwait(true),
                SlashActionIds.ModelPick => SlashActionResult.Completed(await _targets.PickModel!(ModelFilter(request), cancellationToken).ConfigureAwait(true)),
                SlashActionIds.SideOpen => await OpenSideAsync(request, cancellationToken).ConfigureAwait(true),
                _ => ShowHelp(request),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            return SlashActionResult.Failed("The command failed: " + exception.Message);
        }
    }

    private static string ModelFilter(SlashActionRequest request) =>
        request.Argument.Length > 0 ? request.Argument : request.TypedText;

    private static async Task<SlashActionResult> RunTargetAsync(Func<CancellationToken, Task> target, string done, CancellationToken cancellationToken)
    {
        await target(cancellationToken).ConfigureAwait(true);
        return SlashActionResult.Completed(done);
    }

    private SlashActionResult OpenSettings(SlashActionRequest request) =>
        _targets.OpenSettings!(request.Argument)
            ? SlashActionResult.Completed()
            : SlashActionResult.Failed("Settings has no " + request.Argument + " page.");

    private async Task<SlashActionResult> SetModeAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        var failure = await _targets.SetMode!(request.Argument, cancellationToken).ConfigureAwait(true);
        if (failure is not null)
        {
            return SlashActionResult.Failed(failure);
        }

        return SlashActionResult.Completed("Mode: " + request.Argument + ".", request.TypedText.Length > 0 ? request.TypedText : null);
    }

    private async Task<SlashActionResult> OpenSideAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        await _targets.OpenSideChat!(request.TypedText, cancellationToken).ConfigureAwait(true);
        return SlashActionResult.Completed();
    }

    private SlashActionResult ShowHelp(SlashActionRequest request)
    {
        _targets.ShowHelp!(request.TypedText);
        return SlashActionResult.Completed();
    }
}
