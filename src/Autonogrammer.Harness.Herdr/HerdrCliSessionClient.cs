using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Autonogrammer.Harness.Herdr;

/// <summary>
/// The herdr session client for the herdr 0.9.5 command line. A session is one herdr agent. The agent's name is the
/// session identifier, and the agent runs in a pane of a workspace this client creates for it. Every call runs the
/// herdr executable with an argument array through <see cref="IHerdrProcessRunner"/>.
/// </summary>
/// <remarks>
/// Herdr 0.9.5 has no pause, resume, detach or non-interactive attach. Those members fail with
/// <see cref="HerdrOperationUnsupportedException"/> and start no process. <see cref="Capabilities"/> reports all four as absent.
/// The mapping, from the help output and the API schema:
/// <list type="bullet">
/// <item><description>Create: <c>agent get</c> refuses an existing agent with <see cref="HerdrSessionConflictException"/>.
/// Then <c>workspace create --label &lt;id&gt; --no-focus</c>, <c>pane list --workspace &lt;workspace&gt;</c> and
/// <c>agent start &lt;id&gt; --kind &lt;provider&gt; --pane &lt;pane&gt;</c>. A native reference adds <c>-- --resume=&lt;reference&gt;</c>.
/// The new workspace is closed again when the agent does not start.</description></item>
/// <item><description>Status: <c>agent get &lt;id&gt;</c>. Terminate: <c>agent get</c>, then <c>pane close &lt;pane&gt;</c>.</description></item>
/// <item><description>List: <c>agent list</c>. Only named agents are sessions. Heartbeats poll the same listing.</description></item>
/// </list>
/// Herdr reports a missing agent as the error code <c>agent_not_found</c>, which maps to
/// <see cref="HerdrSessionNotFoundException"/>. No other herdr error code has been observed, so any other code surfaces as an
/// <see cref="InvalidOperationException"/> that names the code. Assumed result keys, which the 0.9.5 schema does not
/// publish: <c>workspace.workspace_id</c> in the workspace result, and <c>panes[].pane_id</c> in the pane list result.
/// </remarks>
public sealed class HerdrCliSessionClient : IHerdrSessionClient
{
    private const string AgentNotFoundCode = "agent_not_found";
    private const int ExcerptLength = 200;
    private const string PaneBusyCode = "agent_pane_busy";
    private static readonly TimeSpan PaneReadyBackoff = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan PaneReadyDeadline = TimeSpan.FromSeconds(10);

    private readonly IHerdrProcessRunner _processRunner;
    private readonly string _executablePath;
    private readonly TimeSpan _heartbeatInterval;

    /// <summary>Creates a client that runs the herdr executable through the given process runner.</summary>
    /// <param name="processRunner">Runs each herdr command.</param>
    /// <param name="executablePath">The herdr executable path. It is passed to the runner unchanged.</param>
    /// <param name="heartbeatInterval">The time between two listings of the agents, for the heartbeat stream.</param>
    public HerdrCliSessionClient(IHerdrProcessRunner processRunner, string executablePath, TimeSpan heartbeatInterval)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(heartbeatInterval, TimeSpan.Zero);

        _processRunner = processRunner;
        _executablePath = executablePath;
        _heartbeatInterval = heartbeatInterval;
    }

    /// <inheritdoc />
    public HerdrSessionCapabilities Capabilities { get; } = new(Pause: false, Resume: false, Attach: false, Detach: false);

    /// <inheritdoc />
    public async Task<HerdrSessionSnapshot> CreateAsync(HerdrCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string name = request.Id.Value;

        if (await ReadAgentAsync(request.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new HerdrSessionConflictException($"A herdr agent named {name} already exists.");
        }

        string workspaceId = await ExecuteAsync(
            "workspace create",
            ["workspace", "create", "--label", name, "--no-focus"],
            static result => RequireString(RequireProperty(result, "workspace", JsonValueKind.Object, "workspace create"), "workspace_id", "workspace create"),
            cancellationToken).ConfigureAwait(false);

        try
        {
            string paneId = await ExecuteAsync(
                "pane list",
                ["pane", "list", "--workspace", workspaceId],
                static result => ReadFirstPaneId(result),
                cancellationToken).ConfigureAwait(false);

            await StartAgentAsync(request, paneId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception startFailure)
        {
            await CloseWorkspaceAfterFailedStartAsync(workspaceId, startFailure).ConfigureAwait(false);
            throw;
        }

        return await StatusAsync(request.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Refused. Herdr 0.9.5 has no non-interactive attach; the 'agent attach' command takes over the terminal.</summary>
    /// <param name="sessionId">The session to attach to.</param>
    /// <param name="afterSequence">The last sequence number the caller has seen.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that faults with <see cref="HerdrOperationUnsupportedException"/>.</returns>
    public Task<HerdrSessionSnapshot> AttachAsync(HerdrSessionId sessionId, long afterSequence, CancellationToken cancellationToken) =>
        Task.FromException<HerdrSessionSnapshot>(new HerdrOperationUnsupportedException("Attach"));

    /// <summary>Refused. Herdr 0.9.5 has no detach command.</summary>
    /// <param name="sessionId">The session to detach from.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that faults with <see cref="HerdrOperationUnsupportedException"/>.</returns>
    public Task<HerdrSessionSnapshot> DetachAsync(HerdrSessionId sessionId, CancellationToken cancellationToken) =>
        Task.FromException<HerdrSessionSnapshot>(new HerdrOperationUnsupportedException("Detach"));

    /// <summary>Refused. Herdr 0.9.5 has no pause command for agents or sessions.</summary>
    /// <param name="sessionId">The session to pause.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that faults with <see cref="HerdrOperationUnsupportedException"/>.</returns>
    public Task<HerdrSessionSnapshot> PauseAsync(HerdrSessionId sessionId, CancellationToken cancellationToken) =>
        Task.FromException<HerdrSessionSnapshot>(new HerdrOperationUnsupportedException("Pause"));

    /// <summary>Refused. Herdr 0.9.5 has no resume command for agents or sessions.</summary>
    /// <param name="sessionId">The session to resume.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that faults with <see cref="HerdrOperationUnsupportedException"/>.</returns>
    public Task<HerdrSessionSnapshot> ResumeAsync(HerdrSessionId sessionId, CancellationToken cancellationToken) =>
        Task.FromException<HerdrSessionSnapshot>(new HerdrOperationUnsupportedException("Resume"));

    /// <inheritdoc />
    public async Task<HerdrSessionSnapshot> TerminateAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        AgentView agent = await RequireAgentAsync(sessionId, cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(
            "pane close",
            ["pane", "close", agent.PaneId],
            static _ => true,
            cancellationToken).ConfigureAwait(false);

        return agent.Snapshot with { State = HerdrSessionState.Terminated, IsAttached = false };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HerdrSessionSnapshot>> ListAsync(CancellationToken cancellationToken) =>
        await ExecuteAsync(
            "agent list",
            ["agent", "list"],
            static result => ReadSessions(result),
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<HerdrSessionSnapshot> StatusAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return (await RequireAgentAsync(sessionId, cancellationToken).ConfigureAwait(false)).Snapshot;
    }

    /// <summary>
    /// Streams one beat per live session each time the agents are listed. A listing that fails ends the stream with its
    /// exception. The stream ends with <see cref="OperationCanceledException"/> when the token is cancelled.
    /// </summary>
    /// <param name="cancellationToken">Ends the stream.</param>
    /// <returns>The beats, in the order of each listing.</returns>
    public async IAsyncEnumerable<HerdrHeartbeat> HeartbeatsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            IReadOnlyList<HerdrSessionSnapshot> sessions = await ListAsync(cancellationToken).ConfigureAwait(false);
            foreach (HerdrSessionSnapshot session in sessions)
            {
                yield return new HerdrHeartbeat(session.Id, session.State, session.LastSequence);
            }

            await Task.Delay(_heartbeatInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record AgentView(HerdrSessionSnapshot Snapshot, string PaneId);

    private async Task<AgentView> RequireAgentAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
    {
        AgentView? agent = await ReadAgentAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return agent ?? throw new HerdrSessionNotFoundException($"No herdr session named {sessionId.Value} exists.");
    }

    private async Task<AgentView?> ReadAgentAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteAsync(
                "agent get",
                ["agent", "get", sessionId.Value],
                result => ReadAgentView(sessionId, result),
                cancellationToken).ConfigureAwait(false);
        }
        catch (HerdrSessionNotFoundException)
        {
            return null;
        }
    }

    private async Task CloseWorkspaceAfterFailedStartAsync(string workspaceId, Exception startFailure)
    {
        // Compensation runs without the caller's token: a cancelled create must still leave no empty workspace behind.
        try
        {
            await ExecuteAsync(
                "workspace close",
                ["workspace", "close", workspaceId],
                static _ => true,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception closeFailure) when (closeFailure is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"The agent did not start, and the workspace it created could not be closed: {closeFailure.Message}",
                startFailure);
        }
    }

    /// <summary>
    /// Starts the agent in the pane. A new workspace's root pane can still be starting its shell, so herdr may answer
    /// <c>agent_pane_busy</c>. That answer is retried every <see cref="PaneReadyBackoff"/> until <see cref="PaneReadyDeadline"/>
    /// has passed. Every other answer is final.
    /// </summary>
    private async Task StartAgentAsync(HerdrCreateRequest request, string paneId, CancellationToken cancellationToken)
    {
        string[] arguments = BuildStartArguments(request, paneId);
        int maxRetries = (int)(PaneReadyDeadline.TotalMilliseconds / PaneReadyBackoff.TotalMilliseconds);
        for (int retry = 0; ; retry++)
        {
            HerdrProcessResult process = await _processRunner.RunAsync(_executablePath, arguments, cancellationToken).ConfigureAwait(false);
            if (retry < maxRetries && IsPaneBusy(process))
            {
                await Task.Delay(PaneReadyBackoff, cancellationToken).ConfigureAwait(false);
                continue;
            }

            _ = ReadResult("agent start", process, static _ => true);
            return;
        }
    }

    private static bool IsPaneBusy(HerdrProcessResult process)
    {
        using JsonDocument? envelope = TryParseEnvelope(process.StandardOutput) ?? TryParseEnvelope(process.StandardError);
        if (envelope is null || envelope.RootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return envelope.RootElement.TryGetProperty("error", out JsonElement error)
            && error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("code", out JsonElement code)
            && code.ValueKind == JsonValueKind.String
            && string.Equals(code.GetString(), PaneBusyCode, StringComparison.Ordinal);
    }

    private async Task<T> ExecuteAsync<T>(string operation, string[] arguments, Func<JsonElement, T> readResult, CancellationToken cancellationToken)
    {
        HerdrProcessResult process = await _processRunner.RunAsync(_executablePath, arguments, cancellationToken).ConfigureAwait(false);
        return ReadResult(operation, process, readResult);
    }

    private static T ReadResult<T>(string operation, HerdrProcessResult process, Func<JsonElement, T> readResult)
    {
        using JsonDocument document = ParseEnvelope(operation, process);
        JsonElement root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out JsonElement error))
        {
            throw MapError(operation, error);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"herdr {operation} exited with code {process.ExitCode} without an error response.");
        }

        JsonElement result = RequireProperty(root, "result", JsonValueKind.Object, operation);
        return readResult(result);
    }

    private static JsonDocument ParseEnvelope(string operation, HerdrProcessResult process)
    {
        // A success envelope arrives on stdout. herdr writes an error envelope to stderr, so either stream may hold it.
        if (TryParseEnvelope(process.StandardOutput) is { } fromStdout)
        {
            return fromStdout;
        }

        if (TryParseEnvelope(process.StandardError) is { } fromStderr)
        {
            return fromStderr;
        }

        throw new InvalidOperationException(
            $"herdr {operation} wrote output that is not a JSON envelope (exit code {process.ExitCode}). "
            + $"stdout: {Excerpt(process.StandardOutput)} stderr: {Excerpt(process.StandardError)}");
    }

    private static JsonDocument? TryParseEnvelope(string text)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Excerpt(string text)
    {
        if (text.Length == 0)
        {
            return "(empty)";
        }

        return text.Length <= ExcerptLength ? text : text[..ExcerptLength] + "...";
    }

    private static Exception MapError(string operation, JsonElement error)
    {
        string code = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out JsonElement codeElement)
            && codeElement.ValueKind == JsonValueKind.String
                ? codeElement.GetString() ?? string.Empty
                : string.Empty;
        string message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out JsonElement messageElement)
            && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? string.Empty
                : string.Empty;

        return string.Equals(code, AgentNotFoundCode, StringComparison.Ordinal)
            ? new HerdrSessionNotFoundException(message)
            : new InvalidOperationException($"herdr {operation} failed with code {code}: {message}");
    }

    private static string[] BuildStartArguments(HerdrCreateRequest request, string paneId)
    {
        var arguments = new List<string> { "agent", "start", request.Id.Value, "--kind", request.ProviderId, "--pane", paneId };
        if (request.NativeReference is not null)
        {
            arguments.Add("--");
            arguments.Add("--resume=" + request.NativeReference.Value);
        }

        return [.. arguments];
    }

    private static AgentView ReadAgentView(HerdrSessionId sessionId, JsonElement result)
    {
        JsonElement agent = RequireProperty(result, "agent", JsonValueKind.Object, "agent get");
        return new AgentView(ToSnapshot(sessionId, agent), RequireString(agent, "pane_id", "agent get"));
    }

    private static List<HerdrSessionSnapshot> ReadSessions(JsonElement result)
    {
        JsonElement agents = RequireProperty(result, "agents", JsonValueKind.Array, "agent list");
        var sessions = new List<HerdrSessionSnapshot>();
        foreach (JsonElement agent in agents.EnumerateArray())
        {
            if (agent.ValueKind != JsonValueKind.Object
                || !agent.TryGetProperty("name", out JsonElement nameElement)
                || nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string name = nameElement.GetString() ?? string.Empty;
            if (HerdrSessionId.IsValid(name))
            {
                sessions.Add(ToSnapshot(new HerdrSessionId(name), agent));
            }
        }

        sessions.Sort(static (left, right) => string.CompareOrdinal(left.Id.Value, right.Id.Value));
        return sessions;
    }

    private static string ReadFirstPaneId(JsonElement result)
    {
        JsonElement panes = RequireProperty(result, "panes", JsonValueKind.Array, "pane list");
        if (panes.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("herdr pane list returned no pane for the new workspace.");
        }

        return RequireString(panes[0], "pane_id", "pane list");
    }

    private static HerdrSessionSnapshot ToSnapshot(HerdrSessionId sessionId, JsonElement agent) =>
        new(
            sessionId,
            HerdrSessionState.Running,
            IsAttached: false,
            NativeReference: ReadNativeReference(agent),
            LastSequence: ReadLastSequence(agent));

    private static NativeSessionReference? ReadNativeReference(JsonElement agent)
    {
        if (agent.TryGetProperty("agent_session", out JsonElement session)
            && session.ValueKind == JsonValueKind.Object
            && session.TryGetProperty("value", out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } reference)
        {
            return new NativeSessionReference(reference);
        }

        return null;
    }

    private static long ReadLastSequence(JsonElement agent) =>
        agent.TryGetProperty("state_change_seq", out JsonElement sequence)
            && sequence.ValueKind == JsonValueKind.Number
            && sequence.TryGetInt64(out long value)
                ? value
                : 0;

    private static JsonElement RequireProperty(JsonElement element, string property, JsonValueKind kind, string operation)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == kind)
        {
            return value;
        }

        throw new InvalidOperationException($"herdr {operation} did not return a {property} {kind}.");
    }

    private static string RequireString(JsonElement element, string property, string operation)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text)
        {
            return text;
        }

        throw new InvalidOperationException($"herdr {operation} did not return a {property}.");
    }
}
