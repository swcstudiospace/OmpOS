using OmpOS.Workspace.BuiltinSkills;

namespace OmpOS.Workspace.Conventions;

/// <summary>The file trees a discovery run reads: the one that holds the workspace roots, the one that holds the user's home and, optionally, the skills that ship with the application.</summary>
/// <param name="Project">The source of the workspace roots.</param>
/// <param name="User">The source of the user's home directory. For a remote workspace it is usually still the local machine.</param>
/// <param name="Builtin">The skills that ship with the application, or <see langword="null"/> for none. They have the lowest precedence; see <see cref="ConventionScope.Builtin"/>.</param>
public sealed record ConventionSources(IConventionFileSource Project, IConventionFileSource User, BuiltinSkillSource? Builtin = null)
{
    /// <summary>Creates the sources of a local session: both trees are the local disk, each limited to its own roots.</summary>
    /// <param name="roots">The workspace roots.</param>
    /// <param name="userHome">The user's home directory, or null for none.</param>
    /// <returns>The sources.</returns>
    public static ConventionSources Local(IReadOnlyList<string> roots, string? userHome)
    {
        ArgumentNullException.ThrowIfNull(roots);
        return new ConventionSources(
            new LocalConventionFileSource(roots),
            new LocalConventionFileSource(userHome is null ? Array.Empty<string>() : new[] { userHome }));
    }

    /// <summary>Returns these sources with the skills that ship with the application added.</summary>
    /// <param name="builtin">The bundled skills, or <see langword="null"/> for the <c>skills</c> folder next to the running application (<see cref="BuiltinSkillSource.CreateDefault"/>).</param>
    /// <returns>The sources, with <see cref="Builtin"/> set.</returns>
    public ConventionSources WithBuiltin(BuiltinSkillSource? builtin = null) => this with { Builtin = builtin ?? BuiltinSkillSource.CreateDefault() };
}
