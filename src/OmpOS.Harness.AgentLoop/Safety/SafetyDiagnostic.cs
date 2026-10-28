namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>A problem found while reading a permission or sandbox file. Reading never throws: it reports.</summary>
/// <param name="File">The file the problem is in.</param>
/// <param name="Message">What is wrong and what was done about it.</param>
/// <param name="Severity">Whether the whole file or only an entry was ignored.</param>
public sealed record SafetyDiagnostic(string File, string Message, SafetyDiagnosticSeverity Severity);
