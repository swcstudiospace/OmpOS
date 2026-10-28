using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The Keyboard page: the shortcuts grouped by scope as a read-only list with nothing to edit.</summary>
public sealed class KeyboardSettingsPageTests
{
    /// <summary>The page starts loading and its section is Keyboard; nothing is read before the load.</summary>
    [Fact]
    public void StartsLoadingInItsOwnSection()
    {
        var page = new KeyboardSettingsPage(new ScriptedKeyboardSource());

        page.State.ShouldBe(SettingsPageState.Loading);
        page.Section.ShouldBe(SettingsSection.Keyboard);
        page.Title.ShouldBe(KeyboardCopy.Title);
        page.Groups.ShouldBeEmpty();
    }

    /// <summary>Rows are grouped by scope in the fixed scope order, keeping the source's order inside a group, and an empty scope has no group.</summary>
    [Fact]
    public async Task GroupsTheRowsByScopeInTheFixedOrder()
    {
        var page = new KeyboardSettingsPage(new ScriptedKeyboardSource());

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.Groups.Select(group => group.Scope).ToArray().ShouldBe([KeyboardCopy.ScopeApplication, KeyboardCopy.ScopeAgents, KeyboardCopy.ScopeComposer]);
        page.Groups[0].Rows.Select(row => row.Action).ToArray().ShouldBe(["Open Settings", "Focus the prompt"]);
        page.Groups[1].Rows.Single().Gesture.ShouldBe("Ctrl+N");
        page.IsEmpty.ShouldBeFalse();
    }

    /// <summary>A scope the page does not know follows the known scopes, in the order the source reported it.</summary>
    [Fact]
    public async Task AnUnknownScopeFollowsTheKnownOnes()
    {
        var source = new ScriptedKeyboardSource
        {
            Rows =
            [
                new("Zoom", "Ctrl+=", "Editor"),
                new("Open Settings", "Ctrl+,", KeyboardCopy.ScopeApplication),
                new("Fold", "Ctrl+[", "Editor"),
                new("Pan", "Space", "Canvas"),
            ],
        };
        var page = new KeyboardSettingsPage(source);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Groups.Select(group => group.Scope).ToArray().ShouldBe([KeyboardCopy.ScopeApplication, "Editor", "Canvas"]);
        page.Groups[1].Rows.Select(row => row.Action).ToArray().ShouldBe(["Zoom", "Fold"]);
    }

    /// <summary>A source that reports nothing leaves the page ready and empty, and announces that.</summary>
    [Fact]
    public async Task AnEmptyListIsReadyAndEmpty()
    {
        var page = new KeyboardSettingsPage(new ScriptedKeyboardSource { Rows = [] });
        var announced = PropertyChangeRecorder.Record(page);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.IsEmpty.ShouldBeTrue();
        announced.ShouldAnnounce(nameof(KeyboardSettingsPage.IsEmpty));
    }

    /// <summary>A load failure becomes the error state with the source's reason and no groups.</summary>
    [Fact]
    public async Task ALoadFailureBecomesTheErrorState()
    {
        var page = new KeyboardSettingsPage(new ScriptedKeyboardSource { LoadFailure = "the catalogue is unreadable" });

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("the catalogue is unreadable");
        page.Groups.ShouldBeEmpty();
        page.IsEmpty.ShouldBeFalse();
    }

    /// <summary>Remapping is omitted by design: the page says so, is never dirty, saves nothing and its source has no write.</summary>
    [Fact]
    public async Task RemappingIsOmittedAndSaidSo()
    {
        var token = TestContext.Current.CancellationToken;
        var page = new KeyboardSettingsPage(new ScriptedKeyboardSource());
        await page.LoadAsync(token).ConfigureAwait(true);

        page.Reset();
        page.Discard();

        page.IsDirty.ShouldBeFalse();
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        KeyboardCopy.Description.ShouldContain("remapping", Case.Insensitive);
        typeof(IKeyboardSettingsSource).GetMethods().Select(method => method.Name).ToArray().ShouldBe(["LoadAsync"]);
    }

    /// <summary>A row reads as the action and its keys, for the accessible name.</summary>
    [Fact]
    public void ARowSummaryNamesTheActionAndItsKeys() =>
        new ShortcutRow("Start a new agent", "Ctrl+N", KeyboardCopy.ScopeAgents).Summary.ShouldBe("Start a new agent: Ctrl+N");

    /// <summary>A page without a source is refused.</summary>
    [Fact]
    public void ConstructionRefusesAMissingSource() =>
        Should.Throw<ArgumentNullException>(() => new KeyboardSettingsPage(null!));
}
