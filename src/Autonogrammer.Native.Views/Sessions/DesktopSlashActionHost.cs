using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Commands.Handlers;

namespace Autonogrammer.Native.Views.Sessions;

/// <summary>
/// The desktop head's <see cref="ISlashActionHost"/>: it maps each action id of <see cref="SlashActionIds"/> to the matching callback of <see cref="DesktopSlashTargets"/>, reads the action's
/// argument, and turns the outcome into a sentence for the composer's status line. An action whose callback is null cannot run (<see cref="CanRun"/> is false). The handlers of
/// <see cref="DesktopSlashTargets.Handlers"/> (checkpoints, worktrees, copy, about, workspace sets, display switches and the others) run through a <see cref="SlashActionHandlerHost"/> and win over the callbacks of the same action id.
/// </summary>
public sealed class DesktopSlashActionHost : ISlashActionHost
{
    private readonly ISlashActionHost _host;

    /// <summary>Initializes a new instance of the <see cref="DesktopSlashActionHost"/> class.</summary>
    /// <param name="targets">The callbacks and handlers the actions drive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="targets"/> is null.</exception>
    public DesktopSlashActionHost(DesktopSlashTargets targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        _host = targets.Handlers is { Count: > 0 } handlers ? new SlashActionHandlerHost(handlers, new TargetHost(targets)) : new TargetHost(targets);
    }

    /// <inheritdoc/>
    public bool CanRun(string actionId)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        return _host.CanRun(actionId);
    }

    /// <inheritdoc/>
    public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _host.RunAsync(request, cancellationToken);
    }

    /// <summary>The actions the window runs through plain callbacks.</summary>
    private sealed class TargetHost(DesktopSlashTargets targets) : ISlashActionHost
    {
        public bool CanRun(string actionId)
        {
            ArgumentNullException.ThrowIfNull(actionId);
            return actionId switch
            {
                SlashActionIds.SettingsOpen => targets.OpenSettings is not null,
                SlashActionIds.ChatNew => targets.NewChat is not null,
                SlashActionIds.TerminalNew => targets.NewTerminal is not null,
                SlashActionIds.ModeSet => targets.SetMode is not null,
                SlashActionIds.ModelPick => targets.PickModel is not null,
                SlashActionIds.SideOpen => targets.OpenSideChat is not null,
                SlashActionIds.HelpShow => targets.ShowHelp is not null,
                SlashActionIds.CloudHandoff => targets.HandOffToCloud is not null,
                _ => false,
            };
        }

        public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!CanRun(request.ActionId))
            {
                return SlashActionResult.NotAvailable(SlashPaletteCopy.ActionNotInThisWindow);
            }

            try
            {
                return request.ActionId switch
                {
                    SlashActionIds.SettingsOpen => OpenSettings(request),
                    SlashActionIds.ChatNew => await RunTargetAsync(targets.NewChat!, "Started a new chat.", cancellationToken).ConfigureAwait(true),
                    SlashActionIds.TerminalNew => await RunTargetAsync(targets.NewTerminal!, "Started a new terminal.", cancellationToken).ConfigureAwait(true),
                    SlashActionIds.ModeSet => await SetModeAsync(request, cancellationToken).ConfigureAwait(true),
                    SlashActionIds.ModelPick => SlashActionResult.Completed(await targets.PickModel!(ModelFilter(request), cancellationToken).ConfigureAwait(true)),
                    SlashActionIds.SideOpen => await OpenSideAsync(request, cancellationToken).ConfigureAwait(true),
                    SlashActionIds.CloudHandoff => await targets.HandOffToCloud!(request.TypedText, cancellationToken).ConfigureAwait(true),
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
            targets.OpenSettings!(request.Argument)
                ? SlashActionResult.Completed()
                : SlashActionResult.Failed("Settings has no " + request.Argument + " page.");

        private async Task<SlashActionResult> SetModeAsync(SlashActionRequest request, CancellationToken cancellationToken)
        {
            var failure = await targets.SetMode!(request.Argument, cancellationToken).ConfigureAwait(true);
            if (failure is not null)
            {
                return SlashActionResult.Failed(failure);
            }

            return SlashActionResult.Completed("Mode: " + request.Argument + ".", request.TypedText.Length > 0 ? request.TypedText : null);
        }

        private async Task<SlashActionResult> OpenSideAsync(SlashActionRequest request, CancellationToken cancellationToken)
        {
            await targets.OpenSideChat!(request.TypedText, cancellationToken).ConfigureAwait(true);
            return SlashActionResult.Completed();
        }

        private SlashActionResult ShowHelp(SlashActionRequest request)
        {
            targets.ShowHelp!(request.TypedText);
            return SlashActionResult.Completed();
        }
    }
}
