namespace OmpOS.Presentation.Cloud;

/// <summary>
/// The cloud agents host of a head that has no API base address: it reports <see cref="CloudSetup.NotConfigured"/>, which makes the Cloud page
/// show the setup steps and no agent control, and refuses every other call with <see cref="CloudFailureKind.NotConfigured"/>. It never touches
/// the network.
/// </summary>
public sealed class NotConfiguredCloudHost : ICloudAgentsHost
{
    /// <inheritdoc/>
    public event EventHandler? Changes
    {
        add
        {
        }

        remove
        {
        }
    }

    /// <inheritdoc/>
    public Task<CloudSetup> GetSetupAsync(CancellationToken cancellationToken) =>
        Task.FromResult(CloudSetup.NotConfigured(CloudCopy.NotConfiguredIntro));

    /// <inheritdoc/>
    public Task<CloudLimits> GetLimitsAsync(CancellationToken cancellationToken) => Refuse<CloudLimits>();

    /// <inheritdoc/>
    public Task<IReadOnlyList<CloudAgentRecord>> ListAsync(CancellationToken cancellationToken) => Refuse<IReadOnlyList<CloudAgentRecord>>();

    /// <inheritdoc/>
    public Task<CloudAgentRecord> CreateAsync(CloudAgentRequest request, CancellationToken cancellationToken) => Refuse<CloudAgentRecord>();

    /// <inheritdoc/>
    public Task<CloudAgentRecord> WakeAsync(string agentId, CancellationToken cancellationToken) => Refuse<CloudAgentRecord>();

    /// <inheritdoc/>
    public Task<CloudAgentRecord> StopAsync(string agentId, CancellationToken cancellationToken) => Refuse<CloudAgentRecord>();

    /// <inheritdoc/>
    public Task DeleteAsync(string agentId, CancellationToken cancellationToken) => Refuse<object>();

    /// <inheritdoc/>
    public Task PromptAsync(string agentId, string text, CancellationToken cancellationToken) => Refuse<object>();

    /// <inheritdoc/>
    public Task<CloudPullRequestLink> CreatePullRequestAsync(string agentId, CloudPullRequestRequest request, CancellationToken cancellationToken) =>
        Refuse<CloudPullRequestLink>();

    /// <inheritdoc/>
    public Task OpenPullRequestAsync(CloudPullRequestLink link, CancellationToken cancellationToken) => Refuse<object>();

    private static Task<T> Refuse<T>() =>
        Task.FromException<T>(new CloudHostException(CloudFailureKind.NotConfigured, CloudCopy.NotConfiguredIntro));
}
