using System.Collections.Concurrent;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Harness.Abstractions;
using OmpOS.Sandbox.Abstractions;

namespace OmpOS.Workflows.Worker;

/// <summary>
/// Session-run activities composed over <see cref="ISandboxProvider"/> and <see cref="IHarnessSessionFactory"/>.
/// </summary>
/// <remarks>
/// Mirrors the direct path's activities (provision, run prompt, collect, teardown) so a Temporal run and a
/// direct run issue the same sandbox request, stream the same harness events and tear down the same sandbox.
/// Provisioned sandbox ids are tracked per idempotency key because the provider assigns the sandbox id at
/// provision time; teardown resolves it from that mapping. The worker keeps no session log, so the sequence
/// number returned by <see cref="RunPromptAsync"/> is the count of harness events forwarded for the prompt.
/// Nothing here fabricates a result: registration of both dependencies is required at startup.
/// </remarks>
internal sealed class ComposedSessionRunActivities : ISessionRunActivities
{
    private static readonly TimeSpan ProvisionTimeout = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, string> _sandboxIds = new(StringComparer.Ordinal);
    private readonly ISandboxProvider _sandbox;
    private readonly IHarnessSessionFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="ComposedSessionRunActivities"/> class.</summary>
    /// <param name="sandbox">The sandbox provider that provisions and tears down sandboxes.</param>
    /// <param name="factory">The harness session factory that runs prompts.</param>
    public ComposedSessionRunActivities(ISandboxProvider sandbox, IHarnessSessionFactory factory)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(factory);
        _sandbox = sandbox;
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<SandboxDescriptor> ProvisionSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var result = await _sandbox.ProvisionAsync(CreateProvisionRequest(input), cancellationToken).ConfigureAwait(false);
        _sandboxIds[input.IdempotencyKey] = result.Descriptor.Id;
        return result.Descriptor;
    }

    /// <inheritdoc />
    public async Task<long> RunPromptAsync(SessionRunInput input, Action<string> heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(heartbeat);

        long forwarded = 0;
        var harness = _factory.CreateSession(input.Prompt);
        await using (harness.ConfigureAwait(false))
        {
            await harness.CreateAsync(cancellationToken).ConfigureAwait(false);
            await foreach (var evt in harness.SubmitPromptAsync(input.Prompt, new PromptOptions { Steer = false }, cancellationToken).ConfigureAwait(false))
            {
                // The completion settles the turn and is not forwarded, as on the direct path.
                if (evt is CompletedEvent)
                {
                    break;
                }

                forwarded++;
                heartbeat($"appended:{forwarded}");
            }
        }

        return forwarded;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> CollectArtifactsAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <inheritdoc />
    public Task TeardownSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return _sandboxIds.TryRemove(input.IdempotencyKey, out var sandboxId)
            ? _sandbox.TeardownAsync(sandboxId, cancellationToken)
            : Task.CompletedTask;
    }

    private static ProvisionRequest CreateProvisionRequest(SessionRunInput input)
    {
        return new ProvisionRequest
        {
            IdempotencyKey = input.IdempotencyKey,
            SessionId = input.SessionId,
            Timeout = ProvisionTimeout,
            Isolation = Isolation.Process,
            NetworkPolicy = NetworkPolicy.DefaultDeny,
            Mounts = [],
            SecretRefs = [],
            Spec = new SandboxSpec
            {
                Image = "ompos/in-process",
                Command = ["prompt"],
                WorkingDirectory = "/",
                Environment = new Dictionary<string, string>(StringComparer.Ordinal),
                Limits = null,
            },
        };
    }
}
