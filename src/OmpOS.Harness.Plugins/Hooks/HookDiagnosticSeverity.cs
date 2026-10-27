namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// How serious a problem found while loading hook files is.
/// </summary>
public enum HookDiagnosticSeverity
{
    /// <summary>The file or hook still loads. The Hooks page shows the row.</summary>
    Warning,

    /// <summary>The file or hook was ignored.</summary>
    Error,
}
