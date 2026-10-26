namespace OmpOS.Harness.Acp;

/// <summary>
/// An MCP server the agent is asked to attach to a session through <c>session/new</c> or <c>session/load</c>.
/// The environment variables and headers of the concrete specs carry secrets: no spec prints them in
/// <see cref="object.ToString"/> and this client never logs them.
/// </summary>
/// <param name="Name">The server name. It is the key omp registers the server under, so it must be unique within one session.</param>
public abstract record AcpMcpServer(string Name);
