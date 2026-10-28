namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>What one <c>sandbox.json</c> file says about reading. A value is null when the file does not define it.</summary>
/// <param name="Location">The file.</param>
/// <param name="Exists">True when something is at the path.</param>
/// <param name="IsValid">True when the file was read; false when it was ignored.</param>
/// <param name="ReadBoundary"><c>readBoundary</c>, or null.</param>
/// <param name="AdditionalReadPaths"><c>additionalReadPaths</c>, or null.</param>
/// <param name="Diagnostics">What was ignored and why.</param>
public sealed record SandboxFileDocument(
    SafetyFileLocation Location,
    bool Exists,
    bool IsValid,
    SandboxReadBoundary? ReadBoundary,
    IReadOnlyList<string>? AdditionalReadPaths,
    IReadOnlyList<SafetyDiagnostic> Diagnostics);
