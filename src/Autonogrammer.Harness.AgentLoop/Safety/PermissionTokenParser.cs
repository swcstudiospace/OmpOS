namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Parses the permission tokens of Cursor's permission files: <c>Shell(cmd)</c>, <c>Read(glob)</c>, <c>Write(glob)</c>, <c>WebFetch(domain)</c> and <c>Mcp(server:tool)</c>.</summary>
public static class PermissionTokenParser
{
    /// <summary>The longest token Autonogrammer accepts, in characters.</summary>
    public const int MaxTokenLength = 512;

    /// <summary>Parses one token. It never throws: an invalid text returns the reason.</summary>
    /// <param name="text">The text, for example <c>Shell(git)</c>. The kind name ignores case.</param>
    /// <returns>The token or the reason.</returns>
    public static PermissionTokenParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Invalid("the token is empty");
        }

        var trimmed = text.Trim();
        if (trimmed.Length > MaxTokenLength)
        {
            return Invalid($"the token is longer than {MaxTokenLength} characters");
        }

        var open = trimmed.IndexOf('(', StringComparison.Ordinal);
        if (open <= 0 || trimmed[^1] != ')')
        {
            return Invalid("write a token as Kind(argument), for example Shell(git)");
        }

        if (!TryKind(trimmed[..open], out var kind))
        {
            return Invalid($"'{trimmed[..open]}' is not a token kind; use Shell, Read, Write, WebFetch or Mcp");
        }

        var argument = trimmed[(open + 1)..^1].Trim();
        if (argument.Length == 0)
        {
            return Invalid($"{kind}() has no argument");
        }

        if (argument.Contains('\n', StringComparison.Ordinal) || argument.Contains('\r', StringComparison.Ordinal) || argument.Contains('\0', StringComparison.Ordinal))
        {
            return Invalid("the argument must be one line of text");
        }

        var problem = kind switch
        {
            PermissionTokenKind.Mcp => McpProblem(argument),
            PermissionTokenKind.WebFetch => argument.Contains("://", StringComparison.Ordinal) ? "give a domain such as docs.example.com, not an address" : null,
            _ => null,
        };
        return problem is null ? new PermissionTokenParseResult(new PermissionToken(kind, argument), null) : Invalid(problem);
    }

    private static string? McpProblem(string argument)
    {
        var colon = argument.IndexOf(':', StringComparison.Ordinal);
        return colon <= 0 || colon == argument.Length - 1 ? "write an MCP token as Mcp(server:tool); use * for any" : null;
    }

    private static bool TryKind(string name, out PermissionTokenKind kind)
    {
        switch (name.Trim().ToUpperInvariant())
        {
            case "SHELL":
                kind = PermissionTokenKind.Shell;
                return true;
            case "READ":
                kind = PermissionTokenKind.Read;
                return true;
            case "WRITE":
                kind = PermissionTokenKind.Write;
                return true;
            case "WEBFETCH":
                kind = PermissionTokenKind.WebFetch;
                return true;
            case "MCP":
                kind = PermissionTokenKind.Mcp;
                return true;
            default:
                kind = PermissionTokenKind.Shell;
                return false;
        }
    }

    private static PermissionTokenParseResult Invalid(string reason) => new(null, reason);
}
