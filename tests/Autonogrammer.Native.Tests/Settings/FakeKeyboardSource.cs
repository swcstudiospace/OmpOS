using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>A scripted source of the Keyboard page. It has no save.</summary>
internal sealed class FakeKeyboardSource : FakeSettingsSource<KeyboardShortcutsSnapshot>, IKeyboardSettingsSource
{
    /// <summary>Creates a source that lists one shortcut of each of two scopes, the later scope first.</summary>
    public FakeKeyboardSource()
        : base(new KeyboardShortcutsSnapshot(
        [
            new ShortcutRow("Send the message", "Enter", KeyboardCopy.ScopeComposer),
            new ShortcutRow("Start a new agent", "Ctrl+N", KeyboardCopy.ScopeAgents),
            new ShortcutRow("Open Settings", "Ctrl+,", KeyboardCopy.ScopeApplication),
        ]))
    {
    }

    /// <inheritdoc />
    public Task<KeyboardShortcutsSnapshot> LoadAsync(CancellationToken cancellationToken) => LoadCoreAsync(cancellationToken);
}
