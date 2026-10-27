namespace OmpOS.Workspace.Sets;

/// <summary>
/// The kind of problem a <see cref="WorkspaceSetIssue"/> reports. Heads switch on the code and show the message.
/// </summary>
public enum WorkspaceSetIssueCode
{
    /// <summary>The set has more roots than <see cref="WorkspaceSetValidator.MaxRoots"/>.</summary>
    TooManyRoots,

    /// <summary>A root path is blank, contains NUL, or is not absolute.</summary>
    InvalidPath,

    /// <summary>A local root directory does not exist.</summary>
    MissingDirectory,

    /// <summary>Two roots are the same directory once links and separators are resolved.</summary>
    DuplicateRoot,

    /// <summary>One root lies inside another root.</summary>
    NestedRoot,

    /// <summary>Roots belong to different environments.</summary>
    MixedEnvironments,

    /// <summary>A display name is blank or repeats another after trimming and lowercasing.</summary>
    InvalidName,

    /// <summary>Two roots share an identifier.</summary>
    DuplicateId,

    /// <summary>A local root is not the top of a git working tree, so a run uses it in place instead of in a worktree.</summary>
    NotGitRepository,
}
