namespace OmpOS.Workspace.Conventions;

/// <summary>Why a convention file or folder produced no item, or lost to another item.</summary>
public enum ConventionDiagnosticKind
{
    /// <summary>The file is not valid UTF-8 text or its frontmatter is malformed.</summary>
    Malformed = 0,

    /// <summary>The file is larger than the size cap.</summary>
    TooLarge = 1,

    /// <summary>The file or folder could not be read.</summary>
    Unreadable = 2,

    /// <summary>A symbolic link resolves outside the allowed roots, so the path was refused.</summary>
    EscapesRoot = 3,

    /// <summary>Another item with the same name won by precedence.</summary>
    Duplicate = 4,

    /// <summary>The file is well formed but cannot be used (an unusable name, for example).</summary>
    Invalid = 5,
}
