using System.Globalization;
using System.Text;

namespace Autonogrammer.Harness.Acp;

/// <summary>An MCP server omp starts itself as a child process and talks to over stdio. The wire entry has <c>command</c> and no <c>type</c>.</summary>
/// <param name="Name">The server name. It must be unique within one session.</param>
/// <param name="Command">The executable omp starts.</param>
/// <param name="Args">The command-line arguments, in order.</param>
/// <param name="Env">The environment variables added to the child. The values are secrets.</param>
public sealed record AcpStdioMcpServer(
    string Name,
    string Command,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Env) : AcpMcpServer(Name)
{
    /// <summary>Prints the name, command and counts only: arguments and environment values may be secrets.</summary>
    /// <param name="builder">The builder to append to.</param>
    /// <returns><see langword="true"/>, because members were printed.</returns>
    protected override bool PrintMembers(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Append(CultureInfo.InvariantCulture, $"Name = {Name}, Command = {Command}, ArgumentCount = {Args?.Count ?? 0}, EnvironmentVariableCount = {Env?.Count ?? 0}");
        return true;
    }
}
