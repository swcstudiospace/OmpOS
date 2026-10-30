using System.Globalization;

namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>The sentences the slash action handlers put on the composer's status line.</summary>
public static class HandlerCopy
{
    /// <summary>The sentence for a handler whose target is missing.</summary>
    public const string NotReady = "Open a chat first: this command works on the chat in front.";

    /// <summary>The sentence for rewind when Settings turned it off.</summary>
    public const string RewindOff = "Rewind is off. Turn it on in Settings to use /rewind, /undo and /restore.";

    /// <summary>The sentence for a checkpoint number that is not a number.</summary>
    public const string NeedsNumber = "Type a checkpoint number, for example /rewind 3, or nothing to see the list.";

    /// <summary>The sentence that opens the checkpoint list.</summary>
    public const string CheckpointsOpened = "Choose a checkpoint to rewind the files to. The conversation is not changed.";

    /// <summary>The sentence for nothing to copy.</summary>
    public const string NothingToCopy = "There is nothing to copy yet.";

    /// <summary>The sentence for a missing request id.</summary>
    public const string NoRequestId = "No request has been sent in this chat yet, so there is no request id to copy.";

    /// <summary>The sentence for a missing session id.</summary>
    public const string NoSessionId = "This chat has no session id yet.";

    /// <summary>The sentence for an open command without a path.</summary>
    public const string OpenNeedsPath = "Type the file to open, for example /open src/Program.cs:42.";

    /// <summary>The sentence for an open command with a path that is not inside the workspace.</summary>
    public const string OpenOutside = "That path is not inside the workspace, so it was not opened.";

    /// <summary>The sentence for the Changes bar when nothing changed.</summary>
    public const string NoChanges = "No changes yet. The Changes bar appears when the agent edits a file.";

    /// <summary>The sentence for the Changes bar when it opened.</summary>
    public const string ChangesOpened = "Opened the Changes bar.";

    /// <summary>The sentence for a quit command.</summary>
    public const string Quitting = "Closing Autonogrammer.";

    /// <summary>The sentence for a save-workspace command without a name.</summary>
    public const string WorkspaceNeedsName = "Type a name to save the workspace under, for example /save-workspace backend.";

    /// <summary>The sentence for a workspace name that cannot be used.</summary>
    public const string WorkspaceBadName = "Use a name of up to 80 characters without control characters.";

    /// <summary>The sentence for a best-of-n command without a count.</summary>
    public const string BestOfNeedsCount = "Type the number of candidates and the task, for example /best-of-n 3 fix the failing test.";

    /// <summary>Gets the sentence for the state of a toggle.</summary>
    /// <param name="name">What the toggle controls.</param>
    /// <param name="on">Whether it is on.</param>
    /// <returns>The sentence.</returns>
    public static string Toggle(string name, bool on) => name + ": " + (on ? "on" : "off") + ".";

    /// <summary>Gets the sentence for a copied value.</summary>
    /// <param name="what">What was copied.</param>
    /// <returns>The sentence.</returns>
    public static string Copied(string what) => "Copied " + what + " to the clipboard.";

    /// <summary>Gets the sentence for a clipboard that refused the text.</summary>
    /// <returns>The sentence.</returns>
    public static string ClipboardRefused() => "The clipboard did not take it.";

    /// <summary>Gets the sentence for a folder that opened.</summary>
    /// <param name="path">The folder.</param>
    /// <returns>The sentence.</returns>
    public static string LogsOpened(string path) => "Opened the log folder: " + path;

    /// <summary>Gets the sentence for a folder that did not open.</summary>
    /// <param name="path">The folder.</param>
    /// <returns>The sentence.</returns>
    public static string LogsNotOpened(string path) => "Could not open the log folder: " + path;

    /// <summary>Gets the sentence for the feedback page.</summary>
    /// <returns>The sentence.</returns>
    public static string FeedbackOpened() => "Opened the issue form of Autonogrammer in your browser.";

    /// <summary>Gets the sentence for a feedback page that did not open.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The sentence.</returns>
    public static string FeedbackNotOpened(string address) => "Could not open your browser. Report it at " + address;

    /// <summary>Gets the sentence for a file that opened.</summary>
    /// <param name="path">The workspace path.</param>
    /// <param name="line">The line, or <see langword="null"/>.</param>
    /// <returns>The sentence.</returns>
    public static string FileOpened(string path, int? line) =>
        line is { } number ? string.Create(CultureInfo.InvariantCulture, $"Opened {path} at line {number}.") : "Opened " + path + ".";

    /// <summary>Gets the sentence for a file that did not open.</summary>
    /// <param name="path">The workspace path.</param>
    /// <returns>The sentence.</returns>
    public static string FileNotOpened(string path) => "Could not open " + path + ". Check that the file exists.";

    /// <summary>Gets the sentence for a saved workspace.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The sentence.</returns>
    public static string WorkspaceSaved(string name) => "Saved the open folders as the workspace " + name + ".";

    /// <summary>Gets the sentence for a workspace that opened.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The sentence.</returns>
    public static string WorkspaceLoaded(string name) => "Opened the workspace " + name + ".";

    /// <summary>Gets the sentence for a workspace that does not exist.</summary>
    /// <param name="name">The name.</param>
    /// <param name="known">The names that exist.</param>
    /// <returns>The sentence.</returns>
    public static string WorkspaceUnknown(string name, IReadOnlyList<string> known)
    {
        ArgumentNullException.ThrowIfNull(known);

        return "There is no saved workspace named " + name + "." + (known.Count == 0 ? " None is saved yet." : " Saved: " + string.Join(", ", known) + ".");
    }

    /// <summary>Gets the sentence that lists the saved workspaces.</summary>
    /// <param name="known">The names that exist.</param>
    /// <returns>The sentence.</returns>
    public static string WorkspaceList(IReadOnlyList<string> known)
    {
        ArgumentNullException.ThrowIfNull(known);

        return known.Count == 0 ? "No workspace is saved yet. /save-workspace <name> saves the open folders." : "Saved workspaces: " + string.Join(", ", known) + ". Type /load-workspace <name> to open one.";
    }
}
