using OmpOS.Harness.AgentLoop.Safety;
using OmpOS.Presentation.Safety;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Views.Safety;

/// <summary>
/// Adapts the safety library to the Run mode page. It reads the permission and sandbox files of the user and the open workspace, keeps the run mode in an
/// <see cref="IRunModeStore"/>, and writes only OmpOS-owned files: the allowlists go to <c>&lt;workspace&gt;/.ompos/permissions.json</c> (the user's <c>~/.ompos/permissions.json</c>
/// when no workspace is open) and the read boundary to the matching <c>sandbox.json</c>. A Cursor file is never written.
/// </summary>
public sealed class RunModeSettingsSource : IRunModeSource
{
    private readonly string _home;
    private readonly Func<string?> _projectRoot;
    private readonly IRunModeStore _store;
    private readonly Func<bool> _trustProject;
    private RunModeValues _loaded = RunModeValues.Defaults;

    /// <summary>Initializes a new instance of the <see cref="RunModeSettingsSource"/> class.</summary>
    /// <param name="homeDirectory">The home directory.</param>
    /// <param name="projectRoot">Returns the open workspace's root, or null when none is open. It is called on every load and save.</param>
    /// <param name="store">Where the run mode is kept.</param>
    /// <param name="trustProject">Returns whether the open workspace's allow entries apply. Null trusts every workspace.</param>
    /// <exception cref="ArgumentException"><paramref name="homeDirectory"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="projectRoot"/> or <paramref name="store"/> is null.</exception>
    public RunModeSettingsSource(string homeDirectory, Func<string?> projectRoot, IRunModeStore store, Func<bool>? trustProject = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        ArgumentNullException.ThrowIfNull(projectRoot);
        ArgumentNullException.ThrowIfNull(store);
        _home = homeDirectory;
        _projectRoot = projectRoot;
        _store = store;
        _trustProject = trustProject ?? (static () => true);
    }

    /// <inheritdoc/>
    public async Task<RunModeSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var mode = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var project = ProjectRoot();
        var trust = _trustProject();
        var permissions = PermissionsLoader.Load(_home, project, trust);
        var sandbox = SandboxLoader.Load(_home, project, null, trust);
        var permissionsPath = TargetPath(project, SafetyFileLocations.PermissionsFileName);
        var sandboxPath = TargetPath(project, SafetyFileLocations.SandboxFileName);
        var ownPermissions = permissions.Files.FirstOrDefault(file => string.Equals(file.Location.Path, permissionsPath, StringComparison.Ordinal));
        var ownSandbox = sandbox.Files.FirstOrDefault(file => string.Equals(file.Location.Path, sandboxPath, StringComparison.Ordinal));
        var values = new RunModeValues(
            ToChoice(mode),
            sandbox.ReadBoundary == SandboxReadBoundary.Workspace ? ReadBoundaryChoice.Workspace : ReadBoundaryChoice.System,
            [.. ownPermissions?.TerminalAllowlist ?? []],
            [.. ownPermissions?.McpAllowlist ?? []],
            [.. ownSandbox?.AdditionalReadPaths ?? []]);
        _loaded = values;

        var tokens = permissions.Files
            .Where(file => file.Location.Scope == PermissionScope.User || trust)
            .SelectMany(file => file.AllowTokens.Select(token => new RunModeEntry(token.Text, file.Location.Path, IsEditable: false)))
            .ToList();
        return new RunModeSnapshot
        {
            Values = values,
            EditPermissionsPath = permissionsPath,
            EditSandboxPath = sandboxPath,
            OtherTerminalAllowlist = Others(permissions.TerminalAllowlist, permissionsPath),
            OtherMcpAllowlist = Others(permissions.McpAllowlist, permissionsPath),
            EffectiveReadPaths = [.. sandbox.AdditionalReadPaths.Select(path => new RunModeEntry(path, sandbox.AdditionalReadPathsSource ?? string.Empty, IsEditable: false))],
            AllowRules = tokens,
            DenyRules = [.. permissions.DenyRules.Select(rule => new RunModeEntry(rule.Token.Text, rule.Source, IsEditable: false))],
            AllowInstructions = [.. permissions.AllowInstructions.Select(entry => new RunModeEntry(entry.Text, entry.Source, IsEditable: false))],
            BlockInstructions = [.. permissions.BlockInstructions.Select(entry => new RunModeEntry(entry.Text, entry.Source, IsEditable: false))],
            ReadBoundarySource = sandbox.ReadBoundarySource,
            ReadPathsSource = sandbox.AdditionalReadPathsSource,
            Files =
            [
                .. permissions.Files.Select(file => FileRow(file.Location, file.Exists, file.IsValid, isWinning: false)),
                .. sandbox.Files.Select(file => FileRow(
                    file.Location,
                    file.Exists,
                    file.IsValid,
                    string.Equals(file.Location.Path, sandbox.ReadBoundarySource, StringComparison.Ordinal) || string.Equals(file.Location.Path, sandbox.AdditionalReadPathsSource, StringComparison.Ordinal))),
            ],
            Diagnostics = [.. permissions.Diagnostics.Concat(sandbox.Diagnostics).Select(static d => new RunModeDiagnostic(d.File, d.Message, d.Severity == SafetyDiagnosticSeverity.Error))],
            ProjectTrusted = trust,
        };
    }

    /// <inheritdoc/>
    public async Task SaveAsync(RunModeValues values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        var previous = _loaded;
        var project = ProjectRoot();
        if (values.Mode != previous.Mode)
        {
            Check(await _store.SaveAsync(ToMode(values.Mode), cancellationToken).ConfigureAwait(false));
        }

        var permissionsPath = TargetPath(project, SafetyFileLocations.PermissionsFileName);
        if (!values.TerminalAllowlist.SequenceEqual(previous.TerminalAllowlist, StringComparer.Ordinal))
        {
            Check(PermissionsFileEditor.SetAllowlist(permissionsPath, PermissionsFileEditor.TerminalAllowlistKey, values.TerminalAllowlist));
        }

        if (!values.McpAllowlist.SequenceEqual(previous.McpAllowlist, StringComparer.Ordinal))
        {
            Check(PermissionsFileEditor.SetAllowlist(permissionsPath, PermissionsFileEditor.McpAllowlistKey, values.McpAllowlist));
        }

        if (values.ReadBoundary != previous.ReadBoundary || !values.AdditionalReadPaths.SequenceEqual(previous.AdditionalReadPaths, StringComparer.Ordinal))
        {
            var boundary = values.ReadBoundary == ReadBoundaryChoice.Workspace ? SandboxReadBoundary.Workspace : SandboxReadBoundary.System;
            Check(SandboxFileEditor.Set(TargetPath(project, SafetyFileLocations.SandboxFileName), boundary, values.AdditionalReadPaths));
        }

        _loaded = values;
    }

    private static void Check(SafetyEditResult result)
    {
        if (!result.Succeeded)
        {
            throw new SettingsSourceException(result.Error ?? "The file could not be written.");
        }
    }

    private static RunModeChoice ToChoice(RunMode mode) => mode switch
    {
        RunMode.Allowlist => RunModeChoice.Allowlist,
        RunMode.RunEverything => RunModeChoice.RunEverything,
        _ => RunModeChoice.AutoReview,
    };

    private static RunMode ToMode(RunModeChoice choice) => choice switch
    {
        RunModeChoice.Allowlist => RunMode.Allowlist,
        RunModeChoice.RunEverything => RunMode.RunEverything,
        _ => RunMode.AutoReview,
    };

    private static List<RunModeEntry> Others(IReadOnlyList<SafetyEntry> entries, string targetPath) =>
        [.. entries.Where(entry => !string.Equals(entry.Source, targetPath, StringComparison.Ordinal)).Select(entry => new RunModeEntry(entry.Text, entry.Source, IsEditable: false))];

    private static RunModeFileRow FileRow(SafetyFileLocation location, bool exists, bool isValid, bool isWinning) =>
        new(
            location.Path,
            location.Scope == PermissionScope.User ? SafetyCopy.ScopeUser : SafetyCopy.ScopeProject,
            Path.GetFileName(location.Path),
            !exists ? "Not found." : isValid ? "Read." : "Ignored: see Problems.",
            isWinning);

    private string? ProjectRoot() => _projectRoot() is { Length: > 0 } root ? root : null;

    private string TargetPath(string? project, string fileName) =>
        Path.Combine(project ?? _home, SafetyFileLocations.OmposFolder, fileName);
}
