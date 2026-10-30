using Autonogrammer.Presentation.Commands;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The <see cref="ISlashActionHost"/> of the web head. It runs the native commands the browser can reach (Settings, new chat, mode, model,
/// help). An omp terminal, a side chat and a cloud hand-off are not available in the web head, and say so.
/// </summary>
internal sealed class WebSlashActionHost : ISlashActionHost
{
    /// <summary>The sentence for the commands the web head cannot run.</summary>
    internal const string NotInWebHead = "The web head cannot run this command. Use the desktop app.";

    private readonly WebSlashTargets _targets;

    /// <summary>Initializes a new instance of the <see cref="WebSlashActionHost"/> class.</summary>
    /// <param name="targets">The parts of the head the commands reach.</param>
    internal WebSlashActionHost(WebSlashTargets targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        _targets = targets;
    }

    /// <inheritdoc />
    public bool CanRun(string actionId)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        return actionId switch
        {
            SlashActionIds.SettingsOpen => _targets.OpenSettings is not null,
            SlashActionIds.ChatNew => _targets.NewChat is not null,
            SlashActionIds.ModeSet => _targets.SetMode is not null,
            SlashActionIds.ModelPick => _targets.PickModel is not null,
            SlashActionIds.HelpShow => _targets.ShowHelp is not null,
            _ => false,
        };
    }

    /// <inheritdoc />
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanRun(request.ActionId))
        {
            return SlashActionResult.NotAvailable(NotInWebHead);
        }

        try
        {
            switch (request.ActionId)
            {
                case SlashActionIds.SettingsOpen:
                    return _targets.OpenSettings!(request.Argument)
                        ? SlashActionResult.Completed()
                        : SlashActionResult.Failed("Settings has no " + request.Argument + " page.");
                case SlashActionIds.ChatNew:
                    await _targets.NewChat!(cancellationToken).ConfigureAwait(true);
                    return SlashActionResult.Completed("Started a new chat.");
                case SlashActionIds.ModeSet:
                    var failure = await _targets.SetMode!(request.Argument, cancellationToken).ConfigureAwait(true);
                    return failure is not null
                        ? SlashActionResult.Failed(failure)
                        : SlashActionResult.Completed("Mode: " + request.Argument + ".", request.TypedText.Length > 0 ? request.TypedText : null);
                case SlashActionIds.ModelPick:
                    var filter = request.Argument.Length > 0 ? request.Argument : request.TypedText;
                    return SlashActionResult.Completed(await _targets.PickModel!(filter, cancellationToken).ConfigureAwait(true));
                default:
                    _targets.ShowHelp!(request.TypedText);
                    return SlashActionResult.Completed();
            }
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            return SlashActionResult.Failed("The command failed: " + WebFailureText.Describe(fault));
        }
    }
}
