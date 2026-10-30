namespace Autonogrammer.Workspace.Conventions;

/// <summary>How the indented lines that follow a frontmatter key are read.</summary>
internal enum ConventionFieldMode
{
    /// <summary>The key has a complete scalar on its line, or no value yet.</summary>
    Scalar = 0,

    /// <summary>The key has a block scalar (<c>|</c> or <c>&gt;</c>): indented lines are its text.</summary>
    Block = 1,

    /// <summary>The key has a block list: indented <c>- item</c> lines are its items.</summary>
    List = 2,

    /// <summary>The key has a nested map. Its lines are skipped.</summary>
    Nested = 3,
}
