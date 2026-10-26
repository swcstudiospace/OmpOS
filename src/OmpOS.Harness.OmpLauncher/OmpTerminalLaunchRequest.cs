using OmpOS.Harness.Terminal;

namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// What to open in the terminal: omp, run in the project directory with an explicit argument array.
/// </summary>
/// <param name="ProjectDirectory">The absolute path of the project directory. The session starts there and it must exist.</param>
/// <param name="Arguments">The arguments for omp, one element each. An empty list opens the interactive omp.</param>
/// <param name="Columns">The number of columns the terminal starts with. Must be at least 1.</param>
/// <param name="Rows">The number of rows the terminal starts with. Must be at least 1.</param>
/// <param name="Output">Receives the terminal output as it arrives.</param>
public sealed record OmpTerminalLaunchRequest(
    string ProjectDirectory,
    IReadOnlyList<string> Arguments,
    int Columns,
    int Rows,
    IProcessOutputSink Output);
