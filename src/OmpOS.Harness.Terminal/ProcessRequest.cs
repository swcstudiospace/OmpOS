namespace OmpOS.Harness.Terminal;

/// <summary>
/// Everything a runner needs to start one process. No shell is involved.
/// </summary>
/// <param name="FileName">The executable name or path.</param>
/// <param name="Arguments">The arguments, one element each.</param>
/// <param name="WorkingDirectory">The physical directory to run in.</param>
/// <param name="MaxOutputCharacters">The largest number of characters to keep for each output stream.</param>
public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    int MaxOutputCharacters);
