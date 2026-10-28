namespace Autonogrammer.Presentation.Settings;

/// <summary>Every text of the Display page, in one place so the view carries no literal.</summary>
public static class DisplayCopy
{
    /// <summary>The navigation label of the Display page.</summary>
    public const string Title = "Display";

    /// <summary>The heading of the Display page.</summary>
    public const string Heading = "Display";

    /// <summary>The sentence under the heading.</summary>
    public const string Description =
        "What the prompt, the transcript and the status bar show. A choice is a draft until you save it. The slash commands /vim, /line-numbers, /show-thinking and /status-indicators change the same switches at once.";

    /// <summary>The label of the vim mode switch.</summary>
    public const string VimLabel = "Vim keys in the prompt";

    /// <summary>The sentence under the vim mode switch.</summary>
    public const string VimHelp = "Edit the prompt with vim keys. Off by default.";

    /// <summary>The label of the line numbers switch.</summary>
    public const string LineNumbersLabel = "Line numbers in code blocks";

    /// <summary>The sentence under the line numbers switch.</summary>
    public const string LineNumbersHelp = "Number the lines of code blocks in the transcript.";

    /// <summary>The label of the thinking blocks switch.</summary>
    public const string ThinkingLabel = "Thinking blocks";

    /// <summary>The sentence under the thinking blocks switch.</summary>
    public const string ThinkingHelp = "Show the model's reasoning blocks in the transcript. They are not part of the answer.";

    /// <summary>The reason shown when the display settings could not be read.</summary>
    public const string LoadFailed = "The display settings could not be read.";

    /// <summary>The reason shown when the display settings could not be written.</summary>
    public const string SaveFailed = "The display settings could not be written. Check that the settings folder is writable.";

    /// <summary>The label of the status indicators switch.</summary>
    public const string StatusIndicatorsLabel = "Status indicators";

    /// <summary>The sentence under the status indicators switch.</summary>
    public const string StatusIndicatorsHelp = "Show a status mark next to each session and in the status bar.";

    /// <summary>The label of the running time switch.</summary>
    public const string RunningTimeLabel = "Running time in the status line";

    /// <summary>The sentence under the running time switch.</summary>
    public const string RunningTimeHelp = "Show how long the current turn has been running.";

    /// <summary>The label of the rewind switch.</summary>
    public const string RewindLabel = "Rewind";

    /// <summary>The sentence under the rewind switch.</summary>
    public const string RewindHelp = "Turn on /rewind, /undo and /restore, which restore the files of an earlier turn from the checkpoints Autonogrammer keeps.";

    /// <summary>The accessible name of the group of switches.</summary>
    public const string GroupName = "Display switches";

    /// <summary>The note about what is not offered.</summary>
    public const string NotOfferedNote =
        "Terminal notifications, hints, next-prompt suggestions and a custom status line command are not offered: Autonogrammer has no surface for them.";
}
