using OmpOS.Presentation.Settings;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>A Keyboard source the test scripts: it reports the rows it holds, or fails the load.</summary>
internal sealed class ScriptedKeyboardSource : IKeyboardSettingsSource
{
    /// <summary>Gets or sets the rows the source reports.</summary>
    internal IReadOnlyList<ShortcutRow> Rows { get; set; } =
    [
        new("Send the message", "Enter", KeyboardCopy.ScopeComposer),
        new("Open Settings", "Ctrl+,", KeyboardCopy.ScopeApplication),
        new("Start a new agent", "Ctrl+N", KeyboardCopy.ScopeAgents),
        new("Focus the prompt", "Ctrl+2", KeyboardCopy.ScopeApplication),
    ];

    /// <summary>Gets or sets the reason loads fail with, or null when they succeed.</summary>
    internal string? LoadFailure { get; set; }

    /// <inheritdoc/>
    public Task<KeyboardShortcutsSnapshot> LoadAsync(CancellationToken cancellationToken) =>
        LoadFailure is { } failure
            ? Task.FromException<KeyboardShortcutsSnapshot>(new SettingsSourceException(failure))
            : Task.FromResult(new KeyboardShortcutsSnapshot(Rows));
}
