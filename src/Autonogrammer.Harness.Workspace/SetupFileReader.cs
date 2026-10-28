using System.Text.Json;

namespace Autonogrammer.Workspace;

/// <summary>
/// Reads and validates <c>.autonogrammer/setup.json</c>. A missing file means no setup. A malformed file is reported
/// as a diagnostic, and the runner fails the run rather than starting the agent without its setup.
/// </summary>
internal static class SetupFileReader
{
    /// <summary>
    /// Reads the setup file under the root.
    /// </summary>
    /// <returns>
    /// The setup, no diagnostic and the lowercase hexadecimal SHA-256 of the file bytes that were parsed; or no setup, no
    /// diagnostic and no hash when the file is absent; or no setup, a diagnostic and no hash when it is malformed.
    /// </returns>
    public static (WorkspaceSetup? Setup, WorkspaceDiagnostic? Diagnostic, string? Sha256) Read(WorkspaceRoot root)
    {
        string path;
        try
        {
            path = root.Resolve(WorkspaceConventions.SetupFile);
        }
        catch (WorkspacePathException exception)
        {
            return Rejected(exception.Reason);
        }

        if (Directory.Exists(path))
        {
            return Rejected("it is a directory, not a file");
        }

        if (!File.Exists(path))
        {
            return (null, null, null);
        }

        var (text, hash, readReason) = WorkspaceFiles.ReadTextWithHash(root, WorkspaceConventions.SetupFile);
        if (text is null)
        {
            return Rejected(readReason!);
        }

        SetupFile? file;
        try
        {
            file = JsonSerializer.Deserialize(text, WorkspaceJsonContext.Default.SetupFile);
        }
        catch (JsonException exception)
        {
            return Rejected($"invalid JSON: {exception.Message}");
        }

        if (file is null)
        {
            return Rejected("it must be a JSON object");
        }

        var (setup, reason) = Validate(root, file);
        return reason is null ? (setup, null, hash) : Rejected(reason);
    }

    /// <summary>
    /// Returns whether a command names a path rather than a bare executable name.
    /// </summary>
    internal static bool IsPathCommand(string command) =>
        command.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) || command.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal);

    private static (WorkspaceSetup? Setup, string? Reason) Validate(WorkspaceRoot root, SetupFile file)
    {
        if (file.Steps is null)
        {
            return (null, "it is missing 'steps'");
        }

        var steps = new List<SetupStep>(file.Steps.Count);
        for (var index = 0; index < file.Steps.Count; index++)
        {
            var (step, reason) = ValidateStep(root, file.Steps[index], index + 1);
            if (step is null)
            {
                return (null, reason);
            }

            steps.Add(step);
        }

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in file.Environment ?? new Dictionary<string, string?>(StringComparer.Ordinal))
        {
            if (!WorkspaceConventions.IsValidEnvironmentName(name))
            {
                return (null, $"the environment variable name '{name}' is not valid; use ASCII letters, digits and '_', not starting with a digit");
            }

            if (value is null)
            {
                return (null, $"the environment variable '{name}' has a null value");
            }

            environment[name] = value;
        }

        return (new WorkspaceSetup(steps, environment), null);
    }

    private static (SetupStep? Step, string? Reason) ValidateStep(WorkspaceRoot root, SetupStepFile? step, int position)
    {
        var label = $"step {position}";
        if (step is null)
        {
            return (null, $"{label} is null");
        }

        if (string.IsNullOrWhiteSpace(step.Command) || step.Command.Contains('\0', StringComparison.Ordinal))
        {
            return (null, $"{label} is missing a valid 'command'");
        }

        var arguments = new List<string>();
        foreach (var argument in step.Arguments ?? [])
        {
            if (argument is null || argument.Contains('\0', StringComparison.Ordinal))
            {
                return (null, $"{label} has an invalid argument; arguments must be strings without NUL characters");
            }

            arguments.Add(argument);
        }

        var workingDirectory = step.WorkingDirectory ?? ".";
        if (string.IsNullOrWhiteSpace(workingDirectory) || Path.IsPathRooted(workingDirectory))
        {
            return (null, $"{label} 'workingDirectory' must be a relative path");
        }

        try
        {
            root.Resolve(workingDirectory);
            if (IsPathCommand(step.Command) && !Path.IsPathRooted(step.Command))
            {
                root.Resolve(step.Command);
            }
        }
        catch (WorkspacePathException exception)
        {
            return (null, $"{label} path is rejected: {exception.Reason}");
        }

        return (new SetupStep(step.Command, arguments, workingDirectory), null);
    }

    private static (WorkspaceSetup? Setup, WorkspaceDiagnostic? Diagnostic, string? Sha256) Rejected(string reason) =>
        (null, new WorkspaceDiagnostic(WorkspaceConventions.SetupFile, reason), null);
}
