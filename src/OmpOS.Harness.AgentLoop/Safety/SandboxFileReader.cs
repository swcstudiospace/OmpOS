using System.Text.Json;

namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>
/// Reads the read-access keys of one <c>sandbox.json</c>: <c>readBoundary</c> (<c>system</c> or <c>workspace</c>) and <c>additionalReadPaths</c>.
/// Cursor's other keys (network policy, writable paths) describe an OS sandbox that omp does not have, so they are ignored.
/// Reading never throws: a file that cannot be used is reported and ignored.
/// </summary>
public static class SandboxFileReader
{
    /// <summary>Reads one file.</summary>
    /// <param name="location">The file to read.</param>
    /// <returns>What the file says, or an invalid document with diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public static SandboxFileDocument Read(SafetyFileLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        var diagnostics = new List<SafetyDiagnostic>();
        using var document = SafetyJsonFile.Open(location.Path, diagnostics, out var exists);
        if (document is null)
        {
            return new SandboxFileDocument(location, exists, IsValid: !exists, null, null, diagnostics);
        }

        SandboxReadBoundary? boundary = null;
        if (document.RootElement.TryGetProperty("readBoundary", out var element))
        {
            var text = element.ValueKind == JsonValueKind.String ? element.GetString()?.Trim().ToUpperInvariant() : null;
            switch (text)
            {
                case "SYSTEM":
                    boundary = SandboxReadBoundary.System;
                    break;
                case "WORKSPACE":
                    boundary = SandboxReadBoundary.Workspace;
                    break;
                default:
                    diagnostics.Add(new SafetyDiagnostic(location.Path, "'readBoundary' must be \"system\" or \"workspace\". It is ignored.", SafetyDiagnosticSeverity.Warning));
                    break;
            }
        }

        var paths = SafetyJsonFile.ReadStrings(document.RootElement, "additionalReadPaths", location.Path, diagnostics);
        return new SandboxFileDocument(location, Exists: true, IsValid: true, boundary, paths, diagnostics);
    }
}
