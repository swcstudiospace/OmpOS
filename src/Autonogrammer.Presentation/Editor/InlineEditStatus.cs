namespace Autonogrammer.Presentation.Editor;

/// <summary>The states of an <see cref="InlineEdit"/>.</summary>
public enum InlineEditStatus
{
    /// <summary>The selection and instruction are captured; no replacement has been proposed.</summary>
    AwaitingProposal,

    /// <summary>A replacement is proposed and the buffer is unchanged. The user can accept or reject it.</summary>
    Proposed,

    /// <summary>The proposal replaced the selected range. It can be undone.</summary>
    Accepted,

    /// <summary>The proposal was rejected and the buffer is unchanged.</summary>
    Rejected,

    /// <summary>The accepted proposal was undone and the original text is back in the buffer.</summary>
    Undone,
}
