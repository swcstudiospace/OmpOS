using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>The <see cref="IKeyboardSettingsSource"/> of the web head: the keys the composer and the slash palette answer to. They are fixed; the web head has no key remapping.</summary>
internal sealed class WebKeyboardSettingsSource : IKeyboardSettingsSource
{
    private static readonly ShortcutRow[] Shortcuts =
    [
        new("Send message", "Enter", "Composer"),
        new("New line", "Shift+Enter", "Composer"),
        new("Stop following the response", "Escape", "Composer"),
        new("Choose next slash command", "ArrowDown", "Slash commands"),
        new("Choose previous slash command", "ArrowUp", "Slash commands"),
        new("Put the chosen command in the message", "Enter or Tab", "Slash commands"),
        new("Close the slash command list", "Escape", "Slash commands"),
        new("Close the dialog", "Escape", "Dialogs"),
    ];

    /// <inheritdoc />
    public Task<KeyboardShortcutsSnapshot> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new KeyboardShortcutsSnapshot(Shortcuts));
}
