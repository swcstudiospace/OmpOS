namespace OmpOS.Presentation.Composer;

/// <summary>
/// The draft, send gating and key handling behind the composer. Keys arrive as primitives, so the rules hold
/// with no UI framework; the Razor shell reads the browser key event and passes its name and Shift state.
/// </summary>
/// <remarks>
/// A send still in flight, or one made while a response streams, leaves the draft in place. The draft clears
/// only when the host accepts the text that was sent, so text typed while the send was pending stays.
/// </remarks>
public sealed class ComposerViewModel
{
    private readonly Func<string, Task<bool>> _onSend;
    private readonly Func<Task> _onStop;
    private bool _sending;

    /// <summary>Creates a composer that submits drafts through <paramref name="onSend"/> and stops responses through <paramref name="onStop"/>.</summary>
    /// <param name="onSend">Submits the draft. Resolves to true when the host accepted the send and to false when it refused it.</param>
    /// <param name="onStop">Stops the streaming response.</param>
    /// <exception cref="ArgumentNullException">A callback is null.</exception>
    public ComposerViewModel(Func<string, Task<bool>> onSend, Func<Task> onStop)
    {
        ArgumentNullException.ThrowIfNull(onSend);
        ArgumentNullException.ThrowIfNull(onStop);

        _onSend = onSend;
        _onStop = onStop;
    }

    /// <summary>Gets or sets the draft text the user is composing.</summary>
    public string Draft { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the assistant is streaming. Escape stops only while it is, and the stop control replaces send.</summary>
    public bool IsStreaming { get; set; }

    /// <summary>Submits the draft unless it is blank, a send is already in flight, or a response is streaming.</summary>
    /// <returns>A task that completes when the host has answered the send.</returns>
    public async Task SendAsync()
    {
        if (_sending || IsStreaming || string.IsNullOrWhiteSpace(Draft))
        {
            return;
        }

        var text = Draft;
        _sending = true;
        try
        {
            // Continue on the captured context: the code below writes Draft, which the UI binds to.
            var accepted = await _onSend(text).ConfigureAwait(true);

            // Clear only the message the host accepted. Text typed while the send was pending is not the sent text, so it stays.
            if (accepted && string.Equals(Draft, text, StringComparison.Ordinal))
            {
                Draft = string.Empty;
            }
        }
        finally
        {
            _sending = false;
        }
    }

    /// <summary>Stops the streaming response.</summary>
    /// <returns>A task that completes when the host has handled the stop.</returns>
    public Task StopAsync() => _onStop();

    /// <summary>
    /// Handles one key press in the draft. Enter without Shift sends. Escape stops while a response streams.
    /// Shift+Enter and every other key are left to the text control.
    /// </summary>
    /// <param name="key">The key name as the browser reports it, for example <c>Enter</c> or <c>Escape</c>.</param>
    /// <param name="shift">Whether Shift was held when the key was pressed.</param>
    /// <returns>A task that completes when the resulting send or stop has been handled.</returns>
    public Task HandleKeyAsync(string key, bool shift)
    {
        if (string.Equals(key, "Escape", StringComparison.Ordinal))
        {
            return IsStreaming ? StopAsync() : Task.CompletedTask;
        }

        if (string.Equals(key, "Enter", StringComparison.Ordinal) && !shift)
        {
            return SendAsync();
        }

        return Task.CompletedTask;
    }
}
