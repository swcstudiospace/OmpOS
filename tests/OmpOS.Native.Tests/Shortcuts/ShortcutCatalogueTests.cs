using Avalonia.Input;
using OmpOS.Native.Settings;
using OmpOS.Native.Views.Shortcuts;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Shortcuts;

/// <summary>
/// The shortcut catalogue lists only gestures the shell acts on: each entry is raised in a headless shell and its effect is observed, and a
/// gesture with no test fails here, so the catalogue cannot hold a shortcut that nothing checks.
/// </summary>
public sealed class ShortcutCatalogueTests
{
    /// <summary>Gets the identifier of every catalogue entry, one test case each.</summary>
    public static TheoryData<string> Ids => new(ShortcutCatalogue.Entries.Select(static entry => entry.Id));

    private static KeyGesture GestureOf(string id) => ShortcutCatalogue.Entries.Single(entry => string.Equals(entry.Id, id, StringComparison.Ordinal)).Gesture;

    /// <summary>Every catalogued gesture, raised in the headless shell, has the effect its entry promises.</summary>
    /// <param name="id">The entry.</param>
    [Theory]
    [MemberData(nameof(Ids))]
    public async Task EveryCataloguedGestureHasItsObservableEffect(string id)
    {
        var token = TestContext.Current.CancellationToken;
        var gesture = GestureOf(id);
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() => ObserveAsync(id, gesture, token), token).ConfigureAwait(true);
    }

    private static Task ObserveAsync(string id, KeyGesture gesture, CancellationToken token)
    {
        var focusList = GestureOf(ShortcutIds.FocusAgentList);
        return id switch
        {
            ShortcutIds.OpenSettings => RunAsync(() => ShellShortcutEffects.OpenSettings(gesture)),
            ShortcutIds.NextRegion or ShortcutIds.PreviousRegion => RunAsync(() => ShellShortcutEffects.RegionFocusMoves(gesture)),
            ShortcutIds.FocusAgentList => RunAsync(() => ShellShortcutEffects.FocusAgentList(gesture)),
            ShortcutIds.FocusPrompt => RunAsync(() => ShellShortcutEffects.FocusPrompt(gesture)),
            ShortcutIds.SearchTranscript => RunAsync(() => ShellShortcutEffects.SearchTranscript(gesture)),
            ShortcutIds.SearchConversations => RunAsync(() => ShellShortcutEffects.SearchConversations(gesture)),
            ShortcutIds.Dismiss => RunAsync(() => ShellShortcutEffects.Dismiss(gesture, GestureOf(ShortcutIds.SearchTranscript))),
            ShortcutIds.NewAgent => ShellShortcutEffects.NewAgentAsync(gesture, token),
            ShortcutIds.NewTerminal => ShellShortcutEffects.NewTerminalAsync(gesture, token),
            ShortcutIds.ShowAgentsTab => ShellShortcutEffects.ShowAgentsTabAsync(gesture, token),
            ShortcutIds.ShowKanbanTab => ShellShortcutEffects.ShowKanbanTabAsync(gesture),
            ShortcutIds.OpenAgent => RunAsync(() => ShellShortcutEffects.OpenAgent(gesture, focusList)),
            ShortcutIds.CloseAgent => ShellShortcutEffects.CloseAgentAsync(gesture, focusList, token),
            ShortcutIds.Send => ShellShortcutEffects.SendAsync(gesture, token),
            ShortcutIds.NewLine => ShellShortcutEffects.NewLineAsync(gesture),
            ShortcutIds.Steer => ShellShortcutEffects.SteeringAsync(gesture, SteeringMode.Steer, token),
            ShortcutIds.FollowUp => ShellShortcutEffects.SteeringAsync(gesture, SteeringMode.FollowUp, token),
            ShortcutIds.StopRun => ShellShortcutEffects.StopRunAsync(gesture, token),
            ShortcutIds.CardUp => RunAsync(() => KanbanShortcutEffects.ArrowMovesFocus(gesture, "run-2", "run-1")),
            ShortcutIds.CardDown => RunAsync(() => KanbanShortcutEffects.ArrowMovesFocus(gesture, "run-1", "run-2")),
            ShortcutIds.CardLeft => RunAsync(() => KanbanShortcutEffects.ArrowMovesFocus(gesture, "run-1", "new-1")),
            ShortcutIds.CardRight => RunAsync(() => KanbanShortcutEffects.ArrowMovesFocus(gesture, "new-1", "run-1")),
            ShortcutIds.OpenCard => RunAsync(() => KanbanShortcutEffects.OpenCard(gesture)),
            ShortcutIds.SteerCard => RunAsync(() => KanbanShortcutEffects.SteerCard(gesture)),
            ShortcutIds.CloseSettings or ShortcutIds.CloseSettingsWithControlW => SettingsShortcutEffects.CloseAsync(gesture, token),
            ShortcutIds.FirstSection => SettingsShortcutEffects.SectionEndAsync(gesture, first: true, token),
            ShortcutIds.LastSection => SettingsShortcutEffects.SectionEndAsync(gesture, first: false, token),
            _ => RunAsync(() => Assert.Fail($"The catalogue lists {id}, and no test observes its effect.")),
        };
    }

    private static Task RunAsync(Action effect)
    {
        effect();
        return Task.CompletedTask;
    }

    /// <summary>Every entry has a distinct identifier, a non-empty label, a known scope, and no scope lists a gesture twice.</summary>
    [Fact]
    public void EntriesAreDistinctNamedAndScoped()
    {
        var entries = ShortcutCatalogue.Entries;

        entries.Count.ShouldBeGreaterThan(20);
        entries.Select(static entry => entry.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(entries.Count);
        entries.ShouldAllBe(static entry => !string.IsNullOrWhiteSpace(entry.Action));
        entries.ShouldAllBe(static entry => KeyboardCopy.ScopeOrder.Contains(entry.Scope, StringComparer.Ordinal));
        entries.GroupBy(static entry => (entry.Scope, Text: KeyboardSettingsSource.Format(entry.Gesture)))
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ShouldBeEmpty();
    }

    /// <summary>The gestures the task names as handled by the shell are all catalogued, and each scope has at least one.</summary>
    [Fact]
    public void TheNamedGesturesAreCatalogued()
    {
        var texts = ShortcutCatalogue.Entries.Select(static entry => KeyboardSettingsSource.Format(entry.Gesture)).ToHashSet(StringComparer.Ordinal);

        foreach (var expected in new[] { "Ctrl+,", "Ctrl+N", "Ctrl+Shift+T", "Ctrl+Shift+A", "Ctrl+Shift+K", "Ctrl+1", "Ctrl+2", "F6", "Ctrl+F", "Enter", "Shift+Enter", "Ctrl+Enter", "Ctrl+Shift+Enter" })
        {
            texts.ShouldContain(expected, StringComparer.Ordinal);
        }

        ShortcutCatalogue.Entries.Select(static entry => entry.Scope).Distinct(StringComparer.Ordinal).Count().ShouldBe(KeyboardCopy.ScopeOrder.Count);
    }
}
