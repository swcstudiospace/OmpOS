using System.Globalization;
using System.Text;

namespace Autonogrammer.Harness.Acp;

/// <summary>An MCP server omp reaches over server-sent events. The wire entry is the http entry with <c>type: "sse"</c>.</summary>
/// <param name="Name">The server name. It must be unique within one session.</param>
/// <param name="Url">The absolute http or https endpoint.</param>
/// <param name="Headers">The request headers omp sends. The values are secrets.</param>
public sealed record AcpSseMcpServer(
    string Name,
    Uri Url,
    IReadOnlyDictionary<string, string> Headers) : AcpMcpServer(Name)
{
    /// <summary>Prints the name and header count only: the URL and the header values may be secrets.</summary>
    /// <param name="builder">The builder to append to.</param>
    /// <returns><see langword="true"/>, because members were printed.</returns>
    protected override bool PrintMembers(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Append(CultureInfo.InvariantCulture, $"Name = {Name}, HeaderCount = {Headers?.Count ?? 0}");
        return true;
    }
}
