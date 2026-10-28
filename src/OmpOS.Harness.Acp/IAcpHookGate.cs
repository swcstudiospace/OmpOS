namespace OmpOS.Harness.Acp;

/// <summary>
/// The seam through which hooks see the traffic of an <see cref="AcpAgentSession"/>. The ACP assembly knows nothing about hooks: a head
/// supplies an implementation in <see cref="AcpClientOptions.HookGate"/> and the session calls it at the points below. A null gate is the
/// behaviour without hooks.
/// </summary>
/// <remarks>
/// <para>
/// The <c>Before</c> methods can deny: the session then answers the agent with the reject option or a JSON-RPC error and never performs the
/// action. A gate can only tighten: <see cref="BeforePermissionAsync"/> runs before the <see cref="AcpApprovalPolicy"/>, and the policy still
/// decides what its allowlist admits, so a hook never grants more than the policy does.
/// </para>
/// <para>
/// An implementation must not throw for an expected failure; a method that throws is treated by the session as an allow and a diagnostic.
/// <see cref="Observe"/> must return at once: it runs on the receive loop of the connection.
/// </para>
/// </remarks>
public interface IAcpHookGate
{
    /// <summary>The conversation exists: <c>session/new</c> or <c>session/load</c> answered. Runs before the first prompt can be sent.</summary>
    /// <param name="sessionId">The ACP session identifier.</param>
    /// <param name="resumed">True when the session was loaded, false when it is new.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the gate has processed the event.</returns>
    ValueTask SessionOpenedAsync(string sessionId, bool resumed, CancellationToken cancellationToken);

    /// <summary>A prompt is about to be written as <c>session/prompt</c>. A denial stops the prompt before anything is sent.</summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The verdict. <see cref="AcpHookVerdict.PromptContext"/> is put in front of the prompt when it is not null.</returns>
    ValueTask<AcpHookVerdict> BeforePromptAsync(string prompt, CancellationToken cancellationToken);

    /// <summary>The agent asked for permission (<c>session/request_permission</c>). Runs before the approval policy.</summary>
    /// <param name="request">The request as the policy sees it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The verdict. A denial answers the agent with the reject option and the approval policy is not consulted.</returns>
    ValueTask<AcpHookVerdict> BeforePermissionAsync(AcpPermissionRequest request, CancellationToken cancellationToken);

    /// <summary>The agent reads a file (<c>fs/read_text_file</c>) inside the file root. The content is what the agent would receive for the whole file.</summary>
    /// <param name="fullPath">The absolute path of the file.</param>
    /// <param name="content">The text of the file.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The verdict. A denial answers the agent with a JSON-RPC error and the content is not sent.</returns>
    ValueTask<AcpHookVerdict> BeforeReadFileAsync(string fullPath, string content, CancellationToken cancellationToken);

    /// <summary>The agent wrote a file (<c>fs/write_text_file</c>) inside the file root. The write already happened.</summary>
    /// <param name="fullPath">The absolute path of the file.</param>
    /// <param name="previousContent">The text the file had before the write, or null when it did not exist.</param>
    /// <param name="content">The text that was written.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the gate has processed the event.</returns>
    ValueTask AfterFileWriteAsync(string fullPath, string? previousContent, string content, CancellationToken cancellationToken);

    /// <summary>Reports an observation from <c>session/update</c> traffic. It never blocks and never returns anything.</summary>
    /// <param name="signal">The observation.</param>
    void Observe(AcpHookSignal signal);

    /// <summary>A prompt turn ended. The gate runs the <c>stop</c> hooks and may ask for a follow-up prompt.</summary>
    /// <param name="stopReason">The ACP stop reason (<c>end_turn</c>, <c>cancelled</c>, ...) or <c>error</c> when the turn failed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the conversation continues.</returns>
    ValueTask<AcpTurnEndDecision> TurnEndedAsync(string stopReason, CancellationToken cancellationToken);

    /// <summary>The session is closing or being disposed. The gate finishes the observations it holds and runs the <c>sessionEnd</c> hooks.</summary>
    /// <param name="reason">Why the session ended: <c>user_close</c>, <c>error</c> or <c>completed</c>.</param>
    /// <param name="cancellationToken">Cancels the call; the session bounds it so a slow hook cannot hold the disposal.</param>
    /// <returns>A task that completes when the gate is done.</returns>
    ValueTask SessionClosedAsync(string reason, CancellationToken cancellationToken);
}
