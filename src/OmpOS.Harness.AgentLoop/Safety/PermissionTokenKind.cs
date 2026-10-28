namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>The kind of a permission token: the word before the parenthesis.</summary>
public enum PermissionTokenKind
{
    /// <summary><c>Shell(command)</c>.</summary>
    Shell = 0,

    /// <summary><c>Read(glob)</c>.</summary>
    Read = 1,

    /// <summary><c>Write(glob)</c>.</summary>
    Write = 2,

    /// <summary><c>WebFetch(domain)</c>.</summary>
    WebFetch = 3,

    /// <summary><c>Mcp(server:tool)</c>.</summary>
    Mcp = 4,
}
