using System.Collections.Frozen;
using System.Globalization;
using System.Text;

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
    IProcessOutputSink Output)
{
    private static readonly IReadOnlyDictionary<string, string> NoEnvironment = FrozenDictionary<string, string>.Empty;

    /// <summary>
    /// Extra environment variables for the command, set in addition to the host variables the backend passes on by
    /// itself. An entry here wins over a host variable of the same name. The default is empty. A variable with an empty
    /// value is removed from the command's environment instead of being set to the empty string. Every name must be
    /// non-empty and free of <c>=</c> and NUL, and no value may contain NUL; <see cref="FindEnvironmentProblem"/> reports a breach.
    /// The values are never part of <see cref="object.ToString"/> or of an exception message.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = NoEnvironment;

    /// <summary>
    /// Checks a set of extra environment variables. The result names the first problem found and never contains a value.
    /// </summary>
    /// <param name="environment">The variables to check.</param>
    /// <returns>A description of the first problem, or null when every name and value is acceptable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    public static string? FindEnvironmentProblem(IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        foreach (var (name, value) in environment)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "An environment variable name must not be empty.";
            }

            if (name.Contains('=', StringComparison.Ordinal))
            {
                return $"The environment variable name '{name}' must not contain '='.";
            }

            if (name.Contains('\0', StringComparison.Ordinal))
            {
                return "An environment variable name must not contain a NUL character.";
            }

            if (value is null || value.Contains('\0', StringComparison.Ordinal))
            {
                return $"The value of the environment variable '{name}' must be a string without a NUL character.";
            }
        }

        return null;
    }

    /// <summary>
    /// Prints the session options without the arguments' text and without any environment value: both can hold secrets.
    /// </summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(
            CultureInfo.InvariantCulture,
            $"FileName = {FileName}, Arguments = {Arguments?.Count ?? 0} item(s), WorkingDirectory = {WorkingDirectory}, Columns = {Columns}, Rows = {Rows}, EnvironmentNames = [{string.Join(", ", Environment?.Keys ?? [])}]");
        return true;
    }
}
