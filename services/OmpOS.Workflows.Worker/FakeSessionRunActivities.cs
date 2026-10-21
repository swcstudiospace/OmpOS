using OmpOS.Contracts.Sandboxes;

namespace OmpOS.Workflows.Worker;

/// <summary>
/// Placeholder activities backing the worker until the Sandbox and Sessions layers land.
/// </summary>
/// <remarks>
/// Real implementations arrive with the sandbox/session phases; this fake only satisfies DI
/// so the worker can start and serve the session-run workflow shape for replay and smoke runs.
/// Registration is opt-in only (<c>Workflows:EnableFakeActivities=true</c>); otherwise the
/// worker registers a fail-loud guard so fake success can never be mistaken for real work.
/// </remarks>
internal sealed class FakeSessionRunActivities : ISessionRunActivities
{
    /// <inheritdoc />
    public Task<SandboxDescriptor> ProvisionSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Task.FromResult(new SandboxDescriptor
        {
            Id = input.SandboxId,
            SessionId = input.SessionId,
            Isolation = Isolation.Container,
            Attestation = Attestation.None,
            NativeBinaries = false,
            TtlSeconds = 1800,
            Labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ompos.session"] = input.SessionId,
                ["ompos.idempotency"] = input.IdempotencyKey,
            },
        });
    }

    /// <inheritdoc />
    public Task<long> RunPromptAsync(SessionRunInput input, Action<string> heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(heartbeat);
        heartbeat("seq:1");
        return Task.FromResult(1L);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> CollectArtifactsAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    /// <inheritdoc />
    public Task TeardownSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Task.CompletedTask;
    }
}
