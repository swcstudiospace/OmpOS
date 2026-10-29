using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Presentation.Settings;
using Avalonia.Input;
using Shouldly;

namespace Autonogrammer.Native.Tests.Shortcuts;

/// <summary>What each gesture of the Settings window does, observed on the real window over real page models.</summary>
internal static class SettingsShortcutEffects
{
    /// <summary>Esc or Ctrl+W asks the window to close; with nothing unsaved it closes.</summary>
    /// <param name="gesture">The gesture.</param>
    /// <param name="cancellationToken">Cancels the load of the pages.</param>
    /// <returns>A task that completes when the effect was asserted.</returns>
    internal static async Task CloseAsync(KeyGesture gesture, CancellationToken cancellationToken)
    {
        using var fixture = await SettingsFixture.OpenAsync(cancellationToken).ConfigureAwait(true);
        _ = fixture.Window.NavigationEntry(SettingsSection.Runtime)!.Focus(NavigationMethod.Tab);
        fixture.Settle();
        fixture.Model.IsClosed.ShouldBeFalse();

        ShortcutKeys.Press(fixture.Window, gesture);

        fixture.Model.IsClosed.ShouldBeTrue();
    }

    /// <summary>Home selects the first section and End the last, each showing that page.</summary>
    /// <param name="gesture">The gesture.</param>
    /// <param name="first">True for Home, false for End.</param>
    /// <param name="cancellationToken">Cancels the load of the pages.</param>
    /// <returns>A task that completes when the effect was asserted.</returns>
    internal static async Task SectionEndAsync(KeyGesture gesture, bool first, CancellationToken cancellationToken)
    {
        using var fixture = await SettingsFixture.OpenAsync(cancellationToken).ConfigureAwait(true);
        var start = first ? SettingsSection.Plugins : SettingsSection.Runtime;
        _ = fixture.Show(start);
        _ = fixture.Window.NavigationEntry(start)!.Focus(NavigationMethod.Tab);
        fixture.Settle();

        ShortcutKeys.Press(fixture.Window, gesture);

        fixture.Model.SelectedSection.ShouldBe(first ? SettingsSection.Runtime : SettingsSection.Keyboard);
        fixture.Window.CurrentView.ShouldNotBeNull().DataContext.ShouldBeSameAs(fixture.Model.SelectedPage);
    }
}
