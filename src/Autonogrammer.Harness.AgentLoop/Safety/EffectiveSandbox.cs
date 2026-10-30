namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>
/// The read boundary in force. A <c>sandbox.json</c> value replaces the Settings value, and the project's file wins over the user's;
/// within one place the Autonogrammer file wins over the Cursor file. <c>additionalReadPaths</c> is replaced as a whole, not merged, as in Cursor.
/// An untrusted project can only make the boundary stricter: its <c>workspace</c> boundary applies, its <c>system</c> boundary and its read paths are ignored.
/// </summary>
public sealed class EffectiveSandbox
{
    private EffectiveSandbox(
        SandboxReadBoundary readBoundary,
        string? readBoundarySource,
        IReadOnlyList<string> additionalReadPaths,
        string? additionalReadPathsSource,
        IReadOnlyList<SandboxFileDocument> files,
        IReadOnlyList<SafetyDiagnostic> diagnostics)
    {
        ReadBoundary = readBoundary;
        ReadBoundarySource = readBoundarySource;
        AdditionalReadPaths = additionalReadPaths;
        AdditionalReadPathsSource = additionalReadPathsSource;
        Files = files;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the boundary in force.</summary>
    public SandboxReadBoundary ReadBoundary { get; }

    /// <summary>Gets the path of the file that sets <see cref="ReadBoundary"/>, or null when the Settings value applies.</summary>
    public string? ReadBoundarySource { get; }

    /// <summary>Gets the read allowlist in force, unexpanded.</summary>
    public IReadOnlyList<string> AdditionalReadPaths { get; }

    /// <summary>Gets the path of the file that sets <see cref="AdditionalReadPaths"/>, or null when the Settings value applies.</summary>
    public string? AdditionalReadPathsSource { get; }

    /// <summary>Gets every file that was looked at, lowest precedence first, existing or not.</summary>
    public IReadOnlyList<SandboxFileDocument> Files { get; }

    /// <summary>Gets everything that was ignored while reading and merging.</summary>
    public IReadOnlyList<SafetyDiagnostic> Diagnostics { get; }

    /// <summary>Gets the sandbox of a user with no file and the default Settings.</summary>
    public static EffectiveSandbox Default { get; } = Merge([], SandboxSettings.Default, trustProject: true);

    /// <summary>Merges documents given lowest precedence first over the Settings values.</summary>
    /// <param name="documents">The documents.</param>
    /// <param name="settings">The Settings values.</param>
    /// <param name="trustProject">False to apply only the strict parts of project files.</param>
    /// <returns>The sandbox in force.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static EffectiveSandbox Merge(IReadOnlyList<SandboxFileDocument> documents, SandboxSettings settings, bool trustProject)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(settings);
        var boundary = settings.ReadBoundary;
        string? boundarySource = null;
        IReadOnlyList<string> paths = settings.AdditionalReadPaths;
        string? pathsSource = null;
        var diagnostics = new List<SafetyDiagnostic>();
        foreach (var document in documents)
        {
            diagnostics.AddRange(document.Diagnostics);
            var untrusted = document.Location.Scope == PermissionScope.Project && !trustProject;
            if (document.ReadBoundary is { } fileBoundary)
            {
                if (untrusted && fileBoundary != SandboxReadBoundary.Workspace)
                {
                    diagnostics.Add(new SafetyDiagnostic(document.Location.Path, "The project is not trusted, so its read boundary is ignored unless it is \"workspace\".", SafetyDiagnosticSeverity.Warning));
                }
                else
                {
                    boundary = fileBoundary;
                    boundarySource = document.Location.Path;
                }
            }

            if (document.AdditionalReadPaths is { } filePaths)
            {
                if (untrusted)
                {
                    diagnostics.Add(new SafetyDiagnostic(document.Location.Path, "The project is not trusted, so its read paths are ignored.", SafetyDiagnosticSeverity.Warning));
                }
                else
                {
                    paths = filePaths;
                    pathsSource = document.Location.Path;
                }
            }
        }

        return new EffectiveSandbox(boundary, boundarySource, paths, pathsSource, documents, diagnostics);
    }
}
