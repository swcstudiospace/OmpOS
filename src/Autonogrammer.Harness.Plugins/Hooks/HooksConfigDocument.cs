namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// A parsed hook configuration file.
/// </summary>
/// <param name="Format">The format the file was parsed as.</param>
/// <param name="Version">The <c>version</c> of a <c>hooks.json</c>; 1 for a Claude settings file.</param>
/// <param name="Hooks">The hooks of the <c>hooks</c> object, in file order (event by event, then array order).</param>
/// <param name="OmpHooks">The hooks of the <c>omp</c> section, in file order.</param>
/// <param name="Diagnostics">Every problem found. An error means the file or a hook was ignored.</param>
public sealed record HooksConfigDocument(
    HookConfigFormat Format,
    int Version,
    IReadOnlyList<HookDefinition> Hooks,
    IReadOnlyList<HookDefinition> OmpHooks,
    IReadOnlyList<HookDiagnostic> Diagnostics)
{
    /// <summary>
    /// True when no error diagnostic was recorded.
    /// </summary>
    public bool IsValid => !Diagnostics.Any(diagnostic => diagnostic.Severity == HookDiagnosticSeverity.Error);
}
