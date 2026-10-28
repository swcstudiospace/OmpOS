namespace OmpOS.Native.Composition.Automations;

/// <summary>Opens the agent sessions the runs of local automations happen in. The head implements it over its engine set and panel; a test scripts it.</summary>
internal interface IAutomationSessions
{
    /// <summary>Creates a new agent session for one run, in the registry and in the rail. No prompt is sent yet.</summary>
    /// <param name="title">The title of the session, the automation's name.</param>
    /// <param name="workspaceRoot">The absolute workspace root the run works in.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The session.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> or <paramref name="workspaceRoot"/> is blank.</exception>
    Task<IAutomationSession> CreateAsync(string title, string workspaceRoot, CancellationToken cancellationToken);
}
