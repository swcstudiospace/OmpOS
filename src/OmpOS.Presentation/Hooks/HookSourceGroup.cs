namespace OmpOS.Presentation.Hooks;

/// <summary>The hooks of one file (or one plugin), under its scope heading. The page lists the groups in scope priority order.</summary>
/// <param name="Scope">The scope the file belongs to.</param>
/// <param name="File">The file or plugin the hooks come from.</param>
/// <param name="Rows">The hooks of the file, in run order.</param>
public sealed record HookSourceGroup(HookScopeKind Scope, string File, IReadOnlyList<HookPageRow> Rows)
{
    /// <summary>Gets the heading of the scope.</summary>
    public string ScopeTitle => HooksCopy.ScopeTitle(Scope);
}
