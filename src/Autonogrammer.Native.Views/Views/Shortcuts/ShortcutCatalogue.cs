using Autonogrammer.Presentation.Settings;
using Avalonia.Input;

namespace Autonogrammer.Native.Views.Shortcuts;

/// <summary>
/// The keyboard shortcuts the shell handles today, and nothing else: each entry is a gesture that <c>MainView</c>, the composer, the agent
/// list, the Kanban board or the Settings window really acts on, and the tests raise each one and check what it does. A gesture is added
/// here when its handler exists and a test shows its effect.
/// </summary>
public static class ShortcutCatalogue
{
    /// <summary>Gets the shortcuts in the order the Keyboard page lists them within each scope.</summary>
    public static IReadOnlyList<ShortcutEntry> Entries { get; } =
    [
        new(ShortcutIds.OpenSettings, ShortcutCopy.OpenSettings, new KeyGesture(Key.OemComma, KeyModifiers.Control), KeyboardCopy.ScopeApplication),
        new(ShortcutIds.NextRegion, ShortcutCopy.NextRegion, new KeyGesture(Key.F6), KeyboardCopy.ScopeApplication),
        new(ShortcutIds.PreviousRegion, ShortcutCopy.PreviousRegion, new KeyGesture(Key.F6, KeyModifiers.Shift), KeyboardCopy.ScopeApplication),
        new(ShortcutIds.FocusAgentList, ShortcutCopy.FocusAgentList, new KeyGesture(Key.D1, KeyModifiers.Control), KeyboardCopy.ScopeApplication),
        new(ShortcutIds.FocusPrompt, ShortcutCopy.FocusPrompt, new KeyGesture(Key.D2, KeyModifiers.Control), KeyboardCopy.ScopeApplication),
        new(ShortcutIds.SearchTranscript, ShortcutCopy.SearchTranscript, new KeyGesture(Key.F, KeyModifiers.Control), KeyboardCopy.ScopeApplication),
        new(ShortcutIds.SearchConversations, ShortcutCopy.SearchConversations, new KeyGesture(Key.K, KeyModifiers.Control), KeyboardCopy.ScopeApplication),
        new(ShortcutIds.Dismiss, ShortcutCopy.Dismiss, new KeyGesture(Key.Escape), KeyboardCopy.ScopeApplication),
        new(ShortcutIds.NewAgent, ShortcutCopy.NewAgent, new KeyGesture(Key.N, KeyModifiers.Control), KeyboardCopy.ScopeAgents),
        new(ShortcutIds.NewTerminal, ShortcutCopy.NewTerminal, new KeyGesture(Key.T, KeyModifiers.Control | KeyModifiers.Shift), KeyboardCopy.ScopeAgents),
        new(ShortcutIds.ShowAgentsTab, ShortcutCopy.ShowAgentsTab, new KeyGesture(Key.A, KeyModifiers.Control | KeyModifiers.Shift), KeyboardCopy.ScopeAgents),
        new(ShortcutIds.OpenAgent, ShortcutCopy.OpenAgent, new KeyGesture(Key.Enter), KeyboardCopy.ScopeAgents),
        new(ShortcutIds.CloseAgent, ShortcutCopy.CloseAgent, new KeyGesture(Key.Delete), KeyboardCopy.ScopeAgents),
        new(ShortcutIds.Send, ShortcutCopy.Send, new KeyGesture(Key.Enter), KeyboardCopy.ScopeComposer),
        new(ShortcutIds.NewLine, ShortcutCopy.NewLine, new KeyGesture(Key.Enter, KeyModifiers.Shift), KeyboardCopy.ScopeComposer),
        new(ShortcutIds.Steer, ShortcutCopy.Steer, new KeyGesture(Key.Enter, KeyModifiers.Control), KeyboardCopy.ScopeComposer),
        new(ShortcutIds.FollowUp, ShortcutCopy.FollowUp, new KeyGesture(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift), KeyboardCopy.ScopeComposer),
        new(ShortcutIds.StopRun, ShortcutCopy.StopRun, new KeyGesture(Key.Escape), KeyboardCopy.ScopeComposer),
        new(ShortcutIds.ShowKanbanTab, ShortcutCopy.ShowKanbanTab, new KeyGesture(Key.K, KeyModifiers.Control | KeyModifiers.Shift), KeyboardCopy.ScopeKanban),
        new(ShortcutIds.CardUp, ShortcutCopy.CardUp, new KeyGesture(Key.Up), KeyboardCopy.ScopeKanban),
        new(ShortcutIds.CardDown, ShortcutCopy.CardDown, new KeyGesture(Key.Down), KeyboardCopy.ScopeKanban),
        new(ShortcutIds.CardLeft, ShortcutCopy.CardLeft, new KeyGesture(Key.Left), KeyboardCopy.ScopeKanban),
        new(ShortcutIds.CardRight, ShortcutCopy.CardRight, new KeyGesture(Key.Right), KeyboardCopy.ScopeKanban),
        new(ShortcutIds.OpenCard, ShortcutCopy.OpenCard, new KeyGesture(Key.Enter), KeyboardCopy.ScopeKanban),
        new(ShortcutIds.SteerCard, ShortcutCopy.SteerCard, new KeyGesture(Key.S), KeyboardCopy.ScopeKanban),
        new(ShortcutIds.CloseSettings, ShortcutCopy.CloseSettings, new KeyGesture(Key.Escape), KeyboardCopy.ScopeSettings),
        new(ShortcutIds.CloseSettingsWithControlW, ShortcutCopy.CloseSettingsWithControlW, new KeyGesture(Key.W, KeyModifiers.Control), KeyboardCopy.ScopeSettings),
        new(ShortcutIds.FirstSection, ShortcutCopy.FirstSection, new KeyGesture(Key.Home), KeyboardCopy.ScopeSettings),
        new(ShortcutIds.LastSection, ShortcutCopy.LastSection, new KeyGesture(Key.End), KeyboardCopy.ScopeSettings),
    ];
}
