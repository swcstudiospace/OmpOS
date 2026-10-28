using OmpOS.Api.Automations;
using OmpOS.Contracts.Cloud;

namespace OmpOS.Api.IntegrationTests.Automations;

/// <summary>An <see cref="ICloudAgentLauncher"/> a test controls: it records every launch and either succeeds, fails, or waits until released.</summary>
internal sealed class FakeCloudAgentLauncher : ICloudAgentLauncher
{
    private readonly Lock _gate = new();
    private readonly List<Launch> _launches = [];
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets or sets a failure text every launch fails with, or null to succeed.</summary>
    public string? FailWith { get; set; }

    /// <summary>Gets or sets whether launches wait for <see cref="Release"/>.</summary>
    public bool Hold { get; set; }

    /// <summary>Gets the launches received so far.</summary>
    public IReadOnlyList<Launch> Launches
    {
        get
        {
            lock (_gate)
            {
                return _launches.ToArray();
            }
        }
    }

    /// <summary>Lets every held launch finish.</summary>
    public void Release() => _release.TrySetResult();

    /// <inheritdoc />
    public async Task<CloudLaunchResult> LaunchAsync(string ownerId, CreateCloudAgentRequest request, string prompt, string idempotencyKey, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _launches.Add(new Launch(ownerId, request, prompt, idempotencyKey));
        }

        if (Hold)
        {
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return FailWith is { } failure ? CloudLaunchResult.Failed(failure) : CloudLaunchResult.Started("agent-" + idempotencyKey[..8]);
    }

    /// <summary>One recorded launch.</summary>
    /// <param name="Owner">The owner the agent was created for.</param>
    /// <param name="Request">The agent request.</param>
    /// <param name="Prompt">The prompt.</param>
    /// <param name="Key">The idempotency key.</param>
    internal sealed record Launch(string Owner, CreateCloudAgentRequest Request, string Prompt, string Key);
}
