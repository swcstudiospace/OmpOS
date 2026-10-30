using Autonogrammer.Api.Cloud.Options;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The fallback credential: a fine-grained personal access token from <c>Cloud:Git:Pat</c>. It is long-lived and cannot be narrowed to the
/// repositories and permissions of one run, so the GitHub App is the safer choice; the report and the runbook say so.
/// </summary>
internal sealed class PatCredentialSource : IGitCredentialSource
{
    private readonly IOptionsMonitor<CloudOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="PatCredentialSource"/> class.</summary>
    /// <param name="options">The cloud options.</param>
    public PatCredentialSource(IOptionsMonitor<CloudOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.CurrentValue.Git.Pat);

    /// <inheritdoc />
    public Task<GitCredential> GetAsync(IReadOnlyList<GitRepository> repositories, GitPermissions permissions, CancellationToken cancellationToken)
    {
        var pat = _options.CurrentValue.Git.Pat;
        return string.IsNullOrWhiteSpace(pat)
            ? throw new GitHostException(GitHostFailure.NotConfigured, "No personal access token is configured (Cloud:Git:Pat).")
            : Task.FromResult(new GitCredential(pat, DateTimeOffset.MaxValue));
    }
}
