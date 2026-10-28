namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>One parsed permission token such as <c>Shell(git)</c>, <c>Read(src/**)</c> or <c>Mcp(github:*)</c>.</summary>
/// <param name="Kind">The kind of token.</param>
/// <param name="Argument">The text between the parentheses, trimmed.</param>
public sealed record PermissionToken(PermissionTokenKind Kind, string Argument)
{
    /// <summary>Gets the token as written in a file: <c>Kind(argument)</c>.</summary>
    public string Text => Kind + "(" + Argument + ")";
}
