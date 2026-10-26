namespace OmpOS.Harness.Terminal;

/// <summary>
/// Everything a terminal backend needs to start an interactive session. Nothing is guessed: the executable, the arguments,
/// the working directory and the size are all supplied by the caller, and no shell is chosen for it.
/// </summary>
/// <param name="FileName">The absolute path of the executable. It is not searched for on the path.</param>
/// <param name="Arguments">The arguments, one element each. No element may be null.</param>
/// <param name="WorkingDirectory">The absolute path of an existing directory. The process starts there.</param>
/// <param name="Columns">The number of columns the terminal starts with. Must be at least 1.</param>
/// <param name="Rows">The number of rows the terminal starts with. Must be at least 1.</param>
/// <param name="Output">
/// Receives the terminal output as it arrives. The terminal echoes typed input and the echo is part of this output, so
/// anything typed while echo is on, including a secret, reaches it.
/// </param>
public sealed record TerminalSessionOptions(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    int Columns,
    int Rows,
    IProcessOutputSink Output);
