using System.Globalization;
using System.Text;

namespace Autonogrammer.Harness.AgentLoop.Launch;

/// <summary>
/// An HTTP MCP server omp is given in <c>session/new</c>. The header values may hold a bearer token, so
/// <see cref="ToString"/> prints header names only.
/// </summary>
public sealed record AgentMcpServerSpec
{
    /// <summary>Gets the server name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the address of the server's MCP endpoint.</summary>
    public required Uri Url { get; init; }

    /// <summary>Gets the HTTP headers sent with every request. A value may be secret.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Describes the server without any header value and without any user information, query or fragment of the address.
    /// </summary>
    /// <returns>The name, the address up to its path, and the header names.</returns>
    public override string ToString()
    {
        var address = Url.IsAbsoluteUri ? Url.Scheme + "://" + Url.Authority + Url.AbsolutePath : "(relative)";
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"AgentMcpServerSpec {{ Name = {Name}, Url = {address}, Headers = [");
        builder.AppendJoin(", ", Headers.Keys.Order(StringComparer.Ordinal));
        builder.Append("] }");
        return builder.ToString();
    }
}
