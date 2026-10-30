namespace Autonogrammer.Cowork.Abstractions;

/// <summary>A family of tools the host offers through the one governed seam. No provider talks to omp directly.</summary>
public interface ICoworkToolProvider
{
    /// <summary>Gets the provider id, matching every descriptor it offers.</summary>
    string ProviderId { get; }

    /// <summary>Lists the tools this provider can offer.</summary>
    /// <returns>The descriptors, before availability and profile filtering.</returns>
    IReadOnlyList<CoworkToolDescriptor> Describe();

    /// <summary>Reads whether this provider can serve the session.</summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The availability.</returns>
    ValueTask<CoworkProviderState> GetStateAsync(CoworkSessionContext session, CancellationToken cancellationToken);

    /// <summary>Reads host-observed facts about a call before policy runs.</summary>
    /// <param name="session">The session.</param>
    /// <param name="invocation">The call.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The preview.</returns>
    ValueTask<CoworkCallPreview> PreviewAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken);

    /// <summary>Runs the call.</summary>
    /// <param name="session">The session.</param>
    /// <param name="invocation">The call.</param>
    /// <param name="cancellationToken">Cancels the call. The host also cancels it at the call timeout.</param>
    /// <returns>The result, before host caps.</returns>
    ValueTask<CoworkToolResult> InvokeAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken);

    /// <summary>Releases the profile, pages and grants of the session.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="cancellationToken">Cancels the release.</param>
    /// <returns>A task that completes when the session state is released.</returns>
    ValueTask EndSessionAsync(string sessionId, CancellationToken cancellationToken);
}
