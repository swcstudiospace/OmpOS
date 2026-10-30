using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Autonogrammer.Harness.Terminal;

namespace Autonogrammer.Harness.OmpLauncher;

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
    IProcessOutputSink Output)
{
    private static readonly IReadOnlyDictionary<string, string> NoEnvironment = FrozenDictionary<string, string>.Empty;

    /// <summary>
    /// The environment variables omp is started with, in addition to the few host variables the terminal backend passes on
    /// (see <see cref="TerminalSessionOptions.Environment"/>). The variables of the omp plan are passed here, explicitly.
    /// The default is empty. The values are never part of <see cref="object.ToString"/> or of a launch result.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = NoEnvironment;

    /// <summary>Prints the request without the arguments' text and without any environment value: both can hold secrets.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(
            CultureInfo.InvariantCulture,
            $"ProjectDirectory = {ProjectDirectory}, Arguments = {Arguments?.Count ?? 0} item(s), Columns = {Columns}, Rows = {Rows}, EnvironmentNames = [{string.Join(", ", Environment?.Keys ?? [])}]");
        return true;
    }
}
