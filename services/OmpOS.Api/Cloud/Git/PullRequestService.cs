using Microsoft.Extensions.Options;
using OmpOS.Api.Cloud.Options;
using OmpOS.Contracts.Cloud;

namespace OmpOS.Api.Cloud;

/// <summary>
/// Opens the pull request of a cloud agent. The agent's worker has pushed the branch <c>ompos/cloud/&lt;agent id&gt;</c>; this service checks that the
/// branch exists on the host, refuses to open anything from or into a default branch, opens the pull request ready for review (a draft only when the
/// caller asks and <c>Cloud:Git:PullRequests:NeverDraft</c> is off), redacts the title and body, and records the result. It never clones and never pushes.
/// </summary>
internal sealed class PullRequestService
{
    private const int MaximumTitleLength = 256;
    private const int MaximumBodyLength = 65_536;

    private readonly IGitHostClient _git;
    private readonly IGitCredentialSource _credentials;
    private readonly ICloudAgentStore _store;
    private readonly CloudRedactorFactory _redactors;
    private readonly IOptionsMonitor<CloudOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<PullRequestService> _logger;

    /// <summary>Initializes a new instance of the <see cref="PullRequestService"/> class.</summary>
    /// <param name="git">The git host client.</param>
    /// <param name="credentials">The credential source, asked only whether it is configured.</param>
    /// <param name="store">The agent store.</param>
    /// <param name="redactors">The redactor factory.</param>
    /// <param name="options">The cloud options.</param>
    /// <param name="time">The clock.</param>
    /// <param name="logger">The logger.</param>
    public PullRequestService(
        IGitHostClient git,
        IGitCredentialSource credentials,
        ICloudAgentStore store,
        CloudRedactorFactory redactors,
        IOptionsMonitor<CloudOptions> options,
        TimeProvider time,
        ILogger<PullRequestService> logger)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(redactors);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _git = git;
        _credentials = credentials;
        _store = store;
        _redactors = redactors;
        _options = options;
        _time = time;
        _logger = logger;
    }

    /// <summary>Opens a pull request for the agent's branch in one of its repositories.</summary>
    /// <param name="agent">The agent's record. The caller has already checked ownership.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pull request, or the refusal.</returns>
    public async Task<CloudOutcome<CloudPullRequest>> CreateAsync(CloudAgentRecord agent, CreateCloudPullRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(request);
        var settings = _options.CurrentValue;
        var refusal = Validate(agent, request, settings, out var selected, out var slug);
        if (refusal is not null)
        {
            return Refuse(refusal);
        }

        try
        {
            return await OpenAsync(agent, request, selected!, slug!, settings, cancellationToken).ConfigureAwait(false);
        }
        catch (GitHostException ex)
        {
            Log.PullRequestFailed(_logger, agent.Id, ex.Failure.ToString());
            return Refuse(Map(ex));
        }
    }

    private CloudProblem? Validate(CloudAgentRecord agent, CreateCloudPullRequestRequest request, CloudOptions settings, out CloudRepository? selected, out GitRepository? slug)
    {
        selected = null;
        slug = null;
        if (agent.Status is CloudAgentStatus.Deleting or CloudAgentStatus.Provisioning)
        {
            return CloudProblem.Conflict("agent_not_ready", "Cloud agent is not ready.", $"The agent is {CloudAgentStatusWord.Of(agent.Status)}; it has no branch to open a pull request from.");
        }

        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > MaximumTitleLength)
        {
            return CloudProblem.BadRequest("invalid_title", "Invalid pull request title.", "The title is required and at most 256 characters.");
        }

        if (request.Body is { Length: > MaximumBodyLength })
        {
            return CloudProblem.BadRequest("invalid_body", "Invalid pull request description.", "The description is at most 65536 characters.");
        }

        if (request.BaseBranch is not null && !GitBranchNames.IsValid(request.BaseBranch))
        {
            return CloudProblem.BadRequest("invalid_base_branch", "Invalid base branch.", "The base branch is not an acceptable branch name.");
        }

        selected = Select(agent, request.Repository, settings.Git.AllowedHosts, out slug);
        if (selected is null || slug is null)
        {
            return CloudProblem.BadRequest("unknown_repository", "Unknown repository.", "The repository is not one of the agent's repositories.");
        }

        if (!_credentials.IsConfigured)
        {
            return CloudProblem.Conflict("git_not_configured", "No git credential is configured.", "The server has no GitHub App and no personal access token, so it cannot open a pull request.");
        }

        var cloneUrl = slug.CloneUrl;
        return agent.PullRequests.Any(existing => string.Equals(existing.Repository, cloneUrl, StringComparison.Ordinal))
            ? CloudProblem.Conflict("pull_request_exists", "A pull request already exists.", "The agent already opened a pull request in this repository.")
            : null;
    }

    private async Task<CloudOutcome<CloudPullRequest>> OpenAsync(
        CloudAgentRecord agent,
        CreateCloudPullRequestRequest request,
        CloudRepository selected,
        GitRepository slug,
        CloudOptions settings,
        CancellationToken cancellationToken)
    {
        var defaultBranch = await _git.GetDefaultBranchAsync(slug, cancellationToken).ConfigureAwait(false);
        var baseBranch = request.BaseBranch ?? selected.BaseBranch ?? selected.Branch ?? defaultBranch;
        if (!GitBranchNames.IsValid(baseBranch))
        {
            return Refuse(CloudProblem.BadRequest("invalid_base_branch", "Invalid base branch.", "The base branch is not an acceptable branch name."));
        }

        // A cloud agent never pushes to, and never opens a pull request from, the default branch.
        if (string.Equals(agent.Branch, defaultBranch, StringComparison.Ordinal) || string.Equals(agent.Branch, baseBranch, StringComparison.Ordinal) || !agent.Branch.StartsWith(GitBranchNames.AgentPrefix, StringComparison.Ordinal))
        {
            return Refuse(CloudProblem.Conflict("branch_is_default", "Refusing to open a pull request from this branch.", "The agent's branch must be its own ompos/cloud branch, not the default or the base branch."));
        }

        if (!await _git.BranchExistsAsync(slug, agent.Branch, cancellationToken).ConfigureAwait(false))
        {
            return Refuse(CloudProblem.Conflict("branch_not_pushed", "The branch has not been pushed.", "The agent's branch is not on the git host yet. Ask the agent to commit and push it, then try again."));
        }

        var redactor = await _redactors.CreateAsync(agent, cancellationToken).ConfigureAwait(false);
        var title = request.Title!.Trim();
        var draft = !settings.Git.PullRequests.NeverDraft && request.Draft == true;
        var created = await _git.CreatePullRequestAsync(
            slug,
            new GitPullRequestRequest(redactor.Redact(title) ?? title, redactor.Redact(request.Body), agent.Branch, baseBranch, draft),
            cancellationToken).ConfigureAwait(false);
        var pullRequest = new CloudPullRequest
        {
            Repository = slug.CloneUrl,
            Number = created.Number,
            Url = created.Url,
            Branch = agent.Branch,
            BaseBranch = baseBranch,
            Draft = created.Draft,
            CreatedAt = _time.GetUtcNow(),
        };
        _ = await _store.UpdateAsync(agent.Id, current => current with { PullRequests = [.. current.PullRequests, pullRequest], UpdatedAt = pullRequest.CreatedAt }, cancellationToken).ConfigureAwait(false);
        return CloudOutcome<CloudPullRequest>.Ok(pullRequest);
    }

    /// <summary>Maps a git host failure to the problem the caller sees.</summary>
    /// <param name="ex">The failure.</param>
    /// <returns>The problem.</returns>
    internal static CloudProblem Map(GitHostException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ex.Failure switch
        {
            GitHostFailure.NotConfigured => CloudProblem.Conflict("git_not_configured", "No git credential is configured.", ex.Message),
            GitHostFailure.AlreadyExists => CloudProblem.Conflict("pull_request_exists", "A pull request already exists.", ex.Message),
            GitHostFailure.Unauthorized => new CloudProblem(502, "git_host_unauthorized", "The git host refused the server's credential.", ex.Message),
            GitHostFailure.NotFound => CloudProblem.Conflict("repository_not_visible", "The git host did not find the repository or branch.", ex.Message),
            GitHostFailure.Rejected => new CloudProblem(422, "pull_request_rejected", "The git host refused the pull request.", ex.Message),
            GitHostFailure.Unsupported => CloudProblem.Conflict("git_unsupported", "The request cannot be served.", ex.Message),
            _ => new CloudProblem(503, "git_host_unavailable", "The git host is unavailable.", ex.Message),
        };
    }

    private static CloudRepository? Select(CloudAgentRecord agent, string? requested, IReadOnlyCollection<string> allowedHosts, out GitRepository? slug)
    {
        slug = null;
        if (requested is null)
        {
            var primary = agent.Repositories.Count > 0 ? agent.Repositories[0] : null;
            return primary is not null && GitRepositoryUrl.TryParse(primary.Address, allowedHosts, out slug, out _) ? primary : null;
        }

        if (!GitRepositoryUrl.TryParse(requested, allowedHosts, out var wanted, out _) || wanted is null)
        {
            return null;
        }

        foreach (var repository in agent.Repositories)
        {
            if (GitRepositoryUrl.TryParse(repository.Address, allowedHosts, out var candidate, out _) && candidate is not null
                && string.Equals(candidate.CloneUrl, wanted.CloneUrl, StringComparison.OrdinalIgnoreCase))
            {
                slug = candidate;
                return repository;
            }
        }

        return null;
    }

    private static CloudOutcome<CloudPullRequest> Refuse(CloudProblem problem) => CloudOutcome<CloudPullRequest>.Fail(problem);

    private static class Log
    {
        private static readonly Action<ILogger, string, string, Exception?> PullRequestFailedMessage =
            LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(60, "CloudPullRequestFailed"), "Pull request for cloud agent {AgentId} failed: {Failure}.");

        public static void PullRequestFailed(ILogger logger, string agentId, string failure) => PullRequestFailedMessage(logger, agentId, failure, null);
    }
}
