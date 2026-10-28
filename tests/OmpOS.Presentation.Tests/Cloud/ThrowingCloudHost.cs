using OmpOS.Presentation.Cloud;

namespace OmpOS.Presentation.Tests.Cloud;

/// <summary>A host whose every call throws one exception, for the paths that must survive a failure the host did not translate.</summary>
internal sealed class ThrowingCloudHost : ICloudAgentsHost
{
    private readonly Exception _failure;

    /// <summary>Creates a host that throws <paramref name="failure"/> from every call.</summary>
    /// <param name="failure">What to throw.</param>
    internal ThrowingCloudHost(Exception failure)
    {
        _failure = failure;
    }

    /// <inheritdoc/>
    public event EventHandler? Changes
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public Task<CloudSetup> GetSetupAsync(CancellationToken cancellationToken) => throw _failure;

    /// <inheritdoc/>
    public Task<CloudLimits> GetLimitsAsync(CancellationToken cancellationToken) => throw _failure;

    /// <inheritdoc/>
    public Task<IReadOnlyList<CloudAgentRecord>> ListAsync(CancellationToken cancellationToken) => throw _failure;

    /// <inheritdoc/>
    public Task<CloudAgentRecord> CreateAsync(CloudAgentRequest request, CancellationToken cancellationToken) => throw _failure;

    /// <inheritdoc/>
    public Task<CloudAgentRecord> WakeAsync(string agentId, CancellationToken cancellationToken) => throw _failure;

    /// <inheritdoc/>
    public Task<CloudAgentRecord> StopAsync(string agentId, CancellationToken cancellationToken) => throw _failure;

    /// <inheritdoc/>
    public Task DeleteAsync(string agentId, CancellationToken cancellationToken) => throw _failure;

    /// <inheritdoc/>
    public Task PromptAsync(string agentId, string text, CancellationToken cancellationToken) => throw _failure;

    /// <inheritdoc/>
    public Task<CloudPullRequestLink> CreatePullRequestAsync(string agentId, CloudPullRequestRequest request, CancellationToken cancellationToken) => throw _failure;

    /// <inheritdoc/>
    public Task OpenPullRequestAsync(CloudPullRequestLink link, CancellationToken cancellationToken) => throw _failure;
}
