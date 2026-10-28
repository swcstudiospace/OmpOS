namespace Autonogrammer.Native.Views.Shortcuts;

/// <summary>The stable identifiers of the entries of <see cref="ShortcutCatalogue"/>. A test names an entry by one of these.</summary>
public static class ShortcutIds
{
    /// <summary>Ctrl+, opens the Settings window.</summary>
    public const string OpenSettings = "application.settings";

    /// <summary>F6 moves focus to the next region.</summary>
    public const string NextRegion = "application.region.next";

    /// <summary>Shift+F6 moves focus to the previous region.</summary>
    public const string PreviousRegion = "application.region.previous";

    /// <summary>Ctrl+1 moves focus to the agent list.</summary>
    public const string FocusAgentList = "application.agent-list";

    /// <summary>Ctrl+2 moves focus to the prompt.</summary>
    public const string FocusPrompt = "application.prompt";

    /// <summary>Ctrl+F opens the transcript search.</summary>
    public const string SearchTranscript = "application.search";

    /// <summary>Ctrl or Cmd+K opens the search over all conversations.</summary>
    public const string SearchConversations = "application.search-conversations";

    /// <summary>Esc dismisses the open transcript search or confirmation.</summary>
    public const string Dismiss = "application.dismiss";

    /// <summary>Ctrl+N starts a new agent.</summary>
    public const string NewAgent = "agents.new";

    /// <summary>Ctrl+Shift+T starts a new terminal agent.</summary>
    public const string NewTerminal = "agents.terminal";

    /// <summary>Ctrl+Shift+A shows the Agents tab.</summary>
    public const string ShowAgentsTab = "agents.tab";

    /// <summary>Enter in the agent list opens the selected agent's thread.</summary>
    public const string OpenAgent = "agents.open";

    /// <summary>Delete in the agent list closes the selected agent.</summary>
    public const string CloseAgent = "agents.close";

    /// <summary>Enter in the prompt sends the message.</summary>
    public const string Send = "composer.send";

    /// <summary>Shift+Enter in the prompt adds a line.</summary>
    public const string NewLine = "composer.newline";

    /// <summary>Ctrl+Enter in the prompt steers the active run, or sends when no run is active.</summary>
    public const string Steer = "composer.steer";

    /// <summary>Ctrl+Shift+Enter in the prompt queues a follow-up for the active run.</summary>
    public const string FollowUp = "composer.follow-up";

    /// <summary>Esc in the prompt asks to stop the active run.</summary>
    public const string StopRun = "composer.stop";

    /// <summary>Ctrl+Shift+K shows the Kanban tab.</summary>
    public const string ShowKanbanTab = "kanban.tab";

    /// <summary>Up moves to the card above.</summary>
    public const string CardUp = "kanban.card.up";

    /// <summary>Down moves to the card below.</summary>
    public const string CardDown = "kanban.card.down";

    /// <summary>Left moves to the neighbouring column on the left.</summary>
    public const string CardLeft = "kanban.card.left";

    /// <summary>Right moves to the neighbouring column on the right.</summary>
    public const string CardRight = "kanban.card.right";

    /// <summary>Enter on a card opens its session.</summary>
    public const string OpenCard = "kanban.card.open";

    /// <summary>S on a card opens its steering box.</summary>
    public const string SteerCard = "kanban.card.steer";

    /// <summary>Esc closes the Settings window.</summary>
    public const string CloseSettings = "settings.close.escape";

    /// <summary>Ctrl+W closes the Settings window.</summary>
    public const string CloseSettingsWithControlW = "settings.close.control-w";

    /// <summary>Home in the Settings navigation selects the first section.</summary>
    public const string FirstSection = "settings.section.first";

    /// <summary>End in the Settings navigation selects the last section.</summary>
    public const string LastSection = "settings.section.last";
}
