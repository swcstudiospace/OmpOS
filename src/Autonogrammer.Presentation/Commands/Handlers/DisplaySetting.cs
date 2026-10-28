namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>A display switch the slash commands and the Settings window share.</summary>
public enum DisplaySetting
{
    /// <summary>Vim keys in the composer (Cursor's <c>editor.vimMode</c>).</summary>
    VimMode = 0,

    /// <summary>Line numbers in code blocks (<c>display.showLineNumbers</c>).</summary>
    LineNumbers = 1,

    /// <summary>Thinking blocks in the transcript (<c>display.showThinkingBlocks</c>).</summary>
    ThinkingBlocks = 2,

    /// <summary>Status indicators in the window (<c>display.showStatusIndicators</c>).</summary>
    StatusIndicators = 3,

    /// <summary>The running time on the status line (<c>display.showStatusLineRunningTime</c>).</summary>
    StatusLineRunningTime = 4,

    /// <summary>Whether <c>/rewind</c>, <c>/undo</c> and <c>/restore</c> work (Cursor's <c>rewind</c>).</summary>
    Rewind = 5,
}
