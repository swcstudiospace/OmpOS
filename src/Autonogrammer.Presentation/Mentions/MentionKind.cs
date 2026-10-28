namespace Autonogrammer.Presentation.Mentions;

/// <summary>What an @ or # mention attaches to the message, as Cursor's mention menu names them.</summary>
public enum MentionKind
{
    /// <summary>A file of the workspace.</summary>
    File = 0,

    /// <summary>A folder of the workspace: the message carries the folder's file listing.</summary>
    Folder = 1,

    /// <summary>The output of a terminal session.</summary>
    Terminal = 2,

    /// <summary>The transcript of another chat.</summary>
    Chat = 3,

    /// <summary>The diff of the working state (uncommitted changes).</summary>
    GitDiffWorking = 4,

    /// <summary>The diff of the branch against its base (Cursor's "Branch diff with main").</summary>
    GitDiffBranch = 5,

    /// <summary>The page the built-in browser shows.</summary>
    Browser = 6,

    /// <summary>A rule that is applied only when mentioned.</summary>
    Rule = 7,

    /// <summary>A skill.</summary>
    Skill = 8,

    /// <summary>A side chat: a child conversation of the current chat.</summary>
    SideChat = 9,

    /// <summary>A pull request, mentioned with <c>#</c>.</summary>
    PullRequest = 10,
}
