namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// One problem found while loading a hook file. Loading never throws: an invalid file becomes diagnostics.
/// </summary>
/// <param name="Severity">Whether the file or hook was ignored.</param>
/// <param name="File">The file the problem is in, as shown to the user.</param>
/// <param name="Location">A JSON path such as <c>$.hooks.preToolUse[0].command</c>, or <c>$</c> for the whole file.</param>
/// <param name="Message">What is wrong.</param>
public sealed record HookDiagnostic(HookDiagnosticSeverity Severity, string File, string Location, string Message);
