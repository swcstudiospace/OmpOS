namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>The identifiers of the native actions that <see cref="ISlashActionHandler"/>s run, in addition to the <see cref="SlashActionIds"/> the head already runs. The catalogue names them in its <c>ActionId</c> column.</summary>
public static class HandlerActionIds
{
    /// <summary><c>/rewind [n]</c>: shows the checkpoint timeline, or asks to rewind the files to before checkpoint n.</summary>
    public const string ChatRewind = "chat.rewind";

    /// <summary><c>/undo</c>: asks to rewind the files of the latest checkpoint.</summary>
    public const string ChatUndo = "chat.undo";

    /// <summary><c>/restore [n]</c>: the same as <c>/rewind</c>.</summary>
    public const string ChatRestore = "chat.restore";

    /// <summary><c>/fork</c>, <c>/branch</c>, <c>/duplicate</c>: forks the chat into a new session; with a number, from that message.</summary>
    public const string ChatFork = "chat.fork";

    /// <summary><c>/worktree [name]</c> from the Worktrees panel: starts a run in its own worktree.</summary>
    public const string WorktreeStart = "worktree.start";

    /// <summary><c>/apply-worktree [run] [merge]</c>: applies a worktree's changes to this checkout.</summary>
    public const string WorktreeApply = "worktree.apply";

    /// <summary><c>/delete-worktree [run]</c>: asks to delete a worktree.</summary>
    public const string WorktreeDelete = "worktree.delete";

    /// <summary><c>/best-of-n &lt;n&gt; &lt;task&gt;</c>: runs the task in n worktrees and lists the results.</summary>
    public const string WorktreeBestOfN = "worktree.bestofn";

    /// <summary><c>/copy [user]</c>: copies the last response, or the last message the user sent.</summary>
    public const string ChatCopy = "chat.copy";

    /// <summary><c>/copy-conversation-id</c>: copies the session id.</summary>
    public const string ChatCopyConversationId = "chat.copyConversationId";

    /// <summary><c>/copy-request-id</c>: copies the id of the last request.</summary>
    public const string ChatCopyRequestId = "chat.copyRequestId";

    /// <summary><c>/about</c>: shows the About window.</summary>
    public const string AppAbout = "app.about";

    /// <summary><c>/logs</c>: opens the log folder.</summary>
    public const string AppLogs = "app.logs";

    /// <summary><c>/quit</c>, <c>/exit</c>: closes the application the way its close button does.</summary>
    public const string AppQuit = "app.quit";

    /// <summary><c>/feedback [text]</c>: opens the issue form of the Autonogrammer repository.</summary>
    public const string AppFeedback = "app.feedback";

    /// <summary><c>/open path[:line]</c>: opens a workspace file in the editor.</summary>
    public const string EditorOpen = "editor.open";

    /// <summary><c>/changes</c>: shows the Changes bar.</summary>
    public const string ChangesOpen = "changes.open";

    /// <summary><c>/save-workspace name</c>: saves the open folders as a named workspace.</summary>
    public const string WorkspaceSave = "workspace.save";

    /// <summary><c>/load-workspace [name]</c>: opens a saved workspace, or lists them.</summary>
    public const string WorkspaceLoad = "workspace.load";

    /// <summary><c>/vim [on|off]</c>: toggles Vim keys in the composer.</summary>
    public const string DisplayVim = "display.vim";

    /// <summary><c>/line-numbers [on|off]</c>: toggles line numbers in code blocks.</summary>
    public const string DisplayLineNumbers = "display.lineNumbers";

    /// <summary><c>/show-thinking [on|off]</c>: toggles the display of thinking blocks.</summary>
    public const string DisplayShowThinking = "display.showThinking";

    /// <summary><c>/status-indicators [on|off]</c>: toggles the status indicators of the window.</summary>
    public const string DisplayStatusIndicators = "display.statusIndicators";
}
