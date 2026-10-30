using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Views.Shortcuts;
using Autonogrammer.Presentation.Settings;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>The Keyboard page's source: the shortcut catalogue with each gesture written out in Avalonia's gesture text.</summary>
public sealed class KeyboardSettingsSourceTests
{
    /// <summary>Modifiers come first in Avalonia's order and the key follows, with digits and the comma written as the keys read.</summary>
    [Theory]
    [InlineData(Key.N, KeyModifiers.Control, "Ctrl+N")]
    [InlineData(Key.T, KeyModifiers.Control | KeyModifiers.Shift, "Ctrl+Shift+T")]
    [InlineData(Key.D1, KeyModifiers.Control, "Ctrl+1")]
    [InlineData(Key.D2, KeyModifiers.Control, "Ctrl+2")]
    [InlineData(Key.OemComma, KeyModifiers.Control, "Ctrl+,")]
    [InlineData(Key.F6, KeyModifiers.Shift, "Shift+F6")]
    [InlineData(Key.F6, KeyModifiers.None, "F6")]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift, "Ctrl+Shift+Enter")]
    public void GesturesAreWrittenAsTheKeysRead(Key key, KeyModifiers modifiers, string expected) =>
        KeyboardSettingsSource.Format(new KeyGesture(key, modifiers)).ShouldBe(expected);

    /// <summary>The source lists every catalogue entry once, in catalogue order, with its action, written gesture and scope.</summary>
    [Fact]
    public async Task ListsTheWholeCatalogue()
    {
        var source = new KeyboardSettingsSource();

        var snapshot = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        snapshot.Rows.Count.ShouldBe(ShortcutCatalogue.Entries.Count);
        for (var index = 0; index < snapshot.Rows.Count; index++)
        {
            var entry = ShortcutCatalogue.Entries[index];
            snapshot.Rows[index].ShouldBe(new ShortcutRow(entry.Action, KeyboardSettingsSource.Format(entry.Gesture), entry.Scope));
        }

        snapshot.Rows.ShouldAllBe(static row => row.Gesture.Length > 0 && row.Action.Length > 0);
    }

    /// <summary>A source over its own list reports exactly that list.</summary>
    [Fact]
    public async Task ListsTheEntriesItIsGiven()
    {
        var source = new KeyboardSettingsSource([new ShortcutEntry("x", "Do it", new KeyGesture(Key.X, KeyModifiers.Control), KeyboardCopy.ScopeApplication)]);

        var snapshot = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        snapshot.Rows.ShouldBe([new ShortcutRow("Do it", "Ctrl+X", KeyboardCopy.ScopeApplication)]);
    }

    /// <summary>A source without entries is refused.</summary>
    [Fact]
    public void ConstructionRefusesAMissingList() =>
        Should.Throw<ArgumentNullException>(() => new KeyboardSettingsSource(null!));
}
