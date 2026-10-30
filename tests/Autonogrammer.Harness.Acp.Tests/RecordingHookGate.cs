namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>A hook gate that records every call and answers with whatever the test scripted. Unscripted questions are allowed.</summary>
internal sealed class RecordingHookGate : IAcpHookGate
{
    private readonly List<AcpHookSignal> _signals = [];
    private readonly Lock _gate = new();

    /// <summary>Gets or sets the answer to <see cref="BeforePromptAsync"/>.</summary>
    public Func<string, AcpHookVerdict> OnPrompt { get; set; } = static _ => AcpHookVerdict.Allow;

    /// <summary>Gets or sets the answer to <see cref="BeforePermissionAsync"/>.</summary>
    public Func<AcpPermissionRequest, AcpHookVerdict> OnPermission { get; set; } = static _ => AcpHookVerdict.Allow;

    /// <summary>Gets or sets the answer to <see cref="BeforeReadFileAsync"/>.</summary>
    public Func<string, string, AcpHookVerdict> OnReadFile { get; set; } = static (_, _) => AcpHookVerdict.Allow;

    /// <summary>Gets or sets the answer to <see cref="TurnEndedAsync"/>.</summary>
    public Func<string, AcpTurnEndDecision> OnTurnEnded { get; set; } = static _ => AcpTurnEndDecision.End;

    /// <summary>Gets the sessions that were opened: the identifier and whether it was resumed.</summary>
    public List<(string SessionId, bool Resumed)> Opened { get; } = [];

    /// <summary>Gets the prompts the gate was asked about.</summary>
    public List<string> Prompts { get; } = [];

    /// <summary>Gets the permission requests the gate was asked about.</summary>
    public List<AcpPermissionRequest> Permissions { get; } = [];

    /// <summary>Gets the files the gate was asked about: path and content.</summary>
    public List<(string Path, string Content)> Reads { get; } = [];

    /// <summary>Gets the file writes the gate was told about.</summary>
    public List<(string Path, string? Previous, string Content)> Writes { get; } = [];

    /// <summary>Gets the stop reasons of the turns that ended.</summary>
    public List<string> StopReasons { get; } = [];

    /// <summary>Gets the reasons the session was closed for.</summary>
    public List<string> Closed { get; } = [];

    /// <summary>Gets a snapshot of the observations, in the order they arrived.</summary>
    public IReadOnlyList<AcpHookSignal> Signals
    {
        get
        {
            lock (_gate)
            {
                return [.. _signals];
            }
        }
    }

    /// <inheritdoc />
    public ValueTask SessionOpenedAsync(string sessionId, bool resumed, CancellationToken cancellationToken)
    {
        Opened.Add((sessionId, resumed));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<AcpHookVerdict> BeforePromptAsync(string prompt, CancellationToken cancellationToken)
    {
        Prompts.Add(prompt);
        return ValueTask.FromResult(OnPrompt(prompt));
    }

    /// <inheritdoc />
    public ValueTask<AcpHookVerdict> BeforePermissionAsync(AcpPermissionRequest request, CancellationToken cancellationToken)
    {
        Permissions.Add(request);
        return ValueTask.FromResult(OnPermission(request));
    }

    /// <inheritdoc />
    public ValueTask<AcpHookVerdict> BeforeReadFileAsync(string fullPath, string content, CancellationToken cancellationToken)
    {
        Reads.Add((fullPath, content));
        return ValueTask.FromResult(OnReadFile(fullPath, content));
    }

    /// <inheritdoc />
    public ValueTask AfterFileWriteAsync(string fullPath, string? previousContent, string content, CancellationToken cancellationToken)
    {
        Writes.Add((fullPath, previousContent, content));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Observe(AcpHookSignal signal)
    {
        lock (_gate)
        {
            _signals.Add(signal);
        }
    }

    /// <inheritdoc />
    public ValueTask<AcpTurnEndDecision> TurnEndedAsync(string stopReason, CancellationToken cancellationToken)
    {
        StopReasons.Add(stopReason);
        return ValueTask.FromResult(OnTurnEnded(stopReason));
    }

    /// <inheritdoc />
    public ValueTask SessionClosedAsync(string reason, CancellationToken cancellationToken)
    {
        Closed.Add(reason);
        return ValueTask.CompletedTask;
    }
}
