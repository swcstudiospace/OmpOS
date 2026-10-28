namespace OmpOS.Workspace.Conventions;

/// <summary>Where a discovered convention item lives.</summary>
public enum ConventionScope
{
    /// <summary>A directory of an open workspace root (or a nested folder of it).</summary>
    Project = 0,

    /// <summary>The user's home directory, shared by every workspace.</summary>
    User = 1,

    /// <summary>The application itself: a skill that ships inside it (<see cref="OmpOS.Workspace.BuiltinSkills.BuiltinSkillSource"/>). It has the lowest precedence: a project or user item of the same name wins.</summary>
    Builtin = 2,
}
