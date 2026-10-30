namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// A proposal as the gate holds it, with its status and, once it has run, its result.
/// </summary>
/// <param name="Id">The identifier used to approve or decline the proposal.</param>
/// <param name="Command">The executable name or path.</param>
/// <param name="Arguments">The arguments, exactly as proposed.</param>
/// <param name="WorkingDirectory">The physical directory the command runs in, inside the workspace root.</param>
/// <param name="Status">The lifecycle state.</param>
/// <param name="ExitCode">The process exit code, or null when the command never ran to an exit.</param>
/// <param name="StandardOutput">The captured standard output, bounded by the settings.</param>
/// <param name="StandardError">The captured standard error, bounded by the settings.</param>
/// <param name="StandardOutputTruncated">True when standard output was longer than the bound and was cut.</param>
/// <param name="StandardErrorTruncated">True when standard error was longer than the bound and was cut.</param>
public sealed record TerminalCommand(
    Guid Id,
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    CommandStatus Status,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    bool StandardOutputTruncated,
    bool StandardErrorTruncated);
