namespace OmpOS.Presentation.Commands;

/// <summary>The identifiers of the actions a head runs for <see cref="SlashHandlerKind.NativeAction"/> commands (and the cloud hand-off). A head implements <see cref="ISlashActionHost"/> for the ones it can run.</summary>
public static class SlashActionIds
{
    /// <summary>Opens Settings. The action argument names a section: <c>models</c>, <c>agents</c>, <c>plugins</c>, <c>rules</c>, <c>skills</c> or <c>commands</c>; empty opens the first page.</summary>
    public const string SettingsOpen = "settings.open";

    /// <summary>Starts a new chat.</summary>
    public const string ChatNew = "chat.new";

    /// <summary>Starts a new terminal session.</summary>
    public const string TerminalNew = "terminal.new";

    /// <summary>Switches the session mode to the action argument (<c>plan</c>, <c>ask</c> or <c>debug</c>); text the user typed after the command is returned as the follow-up prompt.</summary>
    public const string ModeSet = "mode.set";

    /// <summary>Filters the model picker by the action argument or the typed text, and selects the model when exactly one matches.</summary>
    public const string ModelPick = "model.pick";

    /// <summary>Opens a side chat; the typed text is its first question.</summary>
    public const string SideOpen = "chats.side.open";

    /// <summary>Opens the slash palette, filtered by the typed text.</summary>
    public const string HelpShow = "help.show";

    /// <summary>Hands the typed task to a cloud agent.</summary>
    public const string CloudHandoff = "cloud.handoff";
}
