namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>
/// Runs <c>/vim</c>, <c>/line-numbers</c>, <c>/show-thinking</c> and <c>/status-indicators</c> over <see cref="DisplaySettings"/>. With <c>on</c> or <c>off</c> the switch is set; with nothing, <c>toggle</c> it is flipped;
/// with <c>status</c> it is only reported. The value is saved at once, so it holds after a restart. When saving fails the switch still holds for this run and the status line says it was not saved.
/// </summary>
public sealed class DisplayToggleSlashHandler : ISlashActionHandler
{
    private readonly DisplaySettings _settings;
    private readonly DisplaySetting _setting;
    private readonly string _name;

    /// <summary>Creates the handler of one action.</summary>
    /// <param name="actionId">One of <see cref="HandlerActionIds.DisplayVim"/>, <see cref="HandlerActionIds.DisplayLineNumbers"/>, <see cref="HandlerActionIds.DisplayShowThinking"/> and <see cref="HandlerActionIds.DisplayStatusIndicators"/>.</param>
    /// <param name="settings">The display switches.</param>
    /// <exception cref="ArgumentException"><paramref name="actionId"/> is not a display action.</exception>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public DisplayToggleSlashHandler(string actionId, DisplaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        ArgumentNullException.ThrowIfNull(settings);
        (_setting, _name) = actionId switch
        {
            HandlerActionIds.DisplayVim => (DisplaySetting.VimMode, "Vim keys in the composer"),
            HandlerActionIds.DisplayLineNumbers => (DisplaySetting.LineNumbers, "Line numbers in code blocks"),
            HandlerActionIds.DisplayShowThinking => (DisplaySetting.ThinkingBlocks, "Thinking blocks"),
            HandlerActionIds.DisplayStatusIndicators => (DisplaySetting.StatusIndicators, "Status indicators"),
            _ => throw new ArgumentException("Not a display action: " + actionId, nameof(actionId)),
        };
        ActionId = actionId;
        _settings = settings;
    }

    /// <inheritdoc/>
    public string ActionId { get; }

    /// <inheritdoc/>
    public bool CanRun => true;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var current = _settings.Get(_setting);
        bool? wanted = request.TypedText.Trim().ToUpperInvariant() switch
        {
            "" or "TOGGLE" => !current,
            "ON" or "TRUE" or "1" => true,
            "OFF" or "FALSE" or "0" => false,
            "STATUS" => current,
            _ => null,
        };
        if (wanted is not { } value)
        {
            return SlashActionResult.Failed(_name + ": type on, off, toggle or status. It is " + (current ? "on" : "off") + " now.");
        }

        if (value == current)
        {
            return SlashActionResult.Completed(HandlerCopy.Toggle(_name, current));
        }

        try
        {
            await _settings.SetAsync(_setting, value, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SlashActionResult.Failed(HandlerCopy.Toggle(_name, value) + " It was not saved: " + exception.Message.TrimEnd('.') + ".");
        }

        return SlashActionResult.Completed(HandlerCopy.Toggle(_name, value));
    }
}
