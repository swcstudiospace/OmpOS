namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>The kind of tool call a <see cref="RunModeRequest"/> describes, in the terms the permission tokens use.</summary>
public enum RunModeToolKind
{
    /// <summary>A call whose kind OmpOS does not recognise. It always asks, except in <see cref="RunMode.RunEverything"/>.</summary>
    Other = 0,

    /// <summary>A shell command (ACP kind <c>execute</c>).</summary>
    Shell = 1,

    /// <summary>A read or search of files (ACP kinds <c>read</c> and <c>search</c>).</summary>
    Read = 2,

    /// <summary>An edit, delete or move of files (ACP kinds <c>edit</c>, <c>delete</c> and <c>move</c>).</summary>
    Write = 3,

    /// <summary>A web fetch (ACP kind <c>fetch</c>).</summary>
    Fetch = 4,

    /// <summary>A call to an MCP server's tool.</summary>
    Mcp = 5,
}
