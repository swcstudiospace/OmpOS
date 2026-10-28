using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Contracts.Cloud;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Checks a create request against the server's rules and produces its normalized form. Every refusal is a 400 problem with a stable code and a
/// sentence that never repeats a value the caller sent (a URL may carry a credential; a name may be a secret by mistake).
/// </summary>
internal sealed class CloudCreateRequestValidator
{
    private const int MaximumRepositories = 16;
    private const int MaximumSecretRefs = 32;
    private const int MaximumNameLength = 100;
    private const int MaximumHintLength = 100;

    private readonly CloudSecretResolver _secrets;

    /// <summary>Initializes a new instance of the <see cref="CloudCreateRequestValidator"/> class.</summary>
    /// <param name="secrets">The secret resolver, to check that each referenced secret exists.</param>
    public CloudCreateRequestValidator(CloudSecretResolver secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        _secrets = secrets;
    }

    /// <summary>Validates a request.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="request">The request.</param>
    /// <param name="options">The cloud options.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The normalized request, or the problem that refused it.</returns>
    public async Task<CloudOutcome<CreateCloudAgentRequest>> ValidateAsync(string ownerId, CreateCloudAgentRequest request, CloudOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        if (request.Repositories is not { Count: > 0 } repositories || repositories.Count > MaximumRepositories)
        {
            return Refuse("invalid_repositories", "Invalid repositories.", "Name between 1 and 16 repositories.");
        }

        var (refusal, normalized, firstName) = NormalizeRepositories(repositories, options);
        if (refusal is not null)
        {
            return refusal;
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = firstName;
        }

        if (name.Length > MaximumNameLength || name.Any(char.IsControl))
        {
            return Refuse("invalid_name", "Invalid name.", "The name is at most 100 characters and has no control characters.");
        }

        if (request.EnvironmentId is not null && !options.Environments.ContainsKey(request.EnvironmentId))
        {
            return Refuse("unknown_environment", "Unknown environment.", "No saved environment has that id on this server.");
        }

        var idle = request.IdleMinutes ?? options.Limits.DefaultIdleMinutes;
        var settingsRefusal = CheckSettings(request, options, idle);
        if (settingsRefusal is not null)
        {
            return settingsRefusal;
        }

        var secrets = await ValidateSecretRefsAsync(ownerId, request, options, cancellationToken).ConfigureAwait(false);
        if (secrets.Problem is not null)
        {
            return CloudOutcome<CreateCloudAgentRequest>.Fail(secrets.Problem);
        }

        var refs = secrets.Value!;

        return CloudOutcome<CreateCloudAgentRequest>.Ok(new CreateCloudAgentRequest
        {
            Name = name,
            Repositories = normalized,
            EnvironmentId = request.EnvironmentId,
            SecretRefs = refs,
            IdleMinutes = idle,
            Model = request.Model?.Trim(),
            Mode = request.Mode?.Trim(),
        });
    }

    private static CloudOutcome<CreateCloudAgentRequest>? CheckSettings(CreateCloudAgentRequest request, CloudOptions options, int idle)
    {
        if (idle < 1 || idle > options.Limits.MaxIdleMinutes)
        {
            return Refuse("invalid_idle_minutes", "Invalid idle window.", "The idle window must be between 1 minute and the server maximum.");
        }

        return IsBadHint(request.Model) || IsBadHint(request.Mode)
            ? Refuse("invalid_hint", "Invalid model or mode.", "The model and the mode are at most 100 printable characters.")
            : null;
    }

    private static (CloudOutcome<CreateCloudAgentRequest>? Refusal, List<CloudRepository> Repositories, string FirstName) NormalizeRepositories(IReadOnlyList<CloudRepository> repositories, CloudOptions options)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<CloudRepository>();
        GitRepository? first = null;
        foreach (var repository in repositories)
        {
            string? problem = null;
            GitRepository? slug = null;
            if (repository is null || !GitRepositoryUrl.TryParse(repository.Address, options.Git.AllowedHosts, out slug, out problem) || slug is null)
            {
                return (Refuse("invalid_repository", "Invalid repository.", problem ?? "A repository entry is empty."), normalized, string.Empty);
            }

            if (!seen.Add(slug.CloneUrl))
            {
                return (Refuse("duplicate_repository", "Duplicate repository.", "A repository is named twice."), normalized, string.Empty);
            }

            if (repository.Branch is not null && !GitBranchNames.IsValid(repository.Branch))
            {
                return (Refuse("invalid_branch", "Invalid branch.", "A branch is not an acceptable branch name."), normalized, string.Empty);
            }

            if (repository.BaseBranch is not null && !GitBranchNames.IsValid(repository.BaseBranch))
            {
                return (Refuse("invalid_branch", "Invalid base branch.", "A base branch is not an acceptable branch name."), normalized, string.Empty);
            }

            first ??= slug;
            normalized.Add(new CloudRepository { Address = slug.CloneUrl, Branch = repository.Branch, BaseBranch = repository.BaseBranch });
        }

        return (null, normalized, first!.FullName);
    }

    private async Task<CloudOutcome<List<string>>> ValidateSecretRefsAsync(string ownerId, CreateCloudAgentRequest request, CloudOptions options, CancellationToken cancellationToken)
    {
        var refs = new List<string>();
        foreach (var secretName in request.SecretRefs ?? [])
        {
            if (!CloudSecretNames.IsValid(secretName) || refs.Count >= MaximumSecretRefs || refs.Contains(secretName, StringComparer.Ordinal))
            {
                return CloudOutcome<List<string>>.Fail(CloudProblem.BadRequest("invalid_secret_ref", "Invalid secret reference.", "A secret reference is not a valid name, is repeated, or there are more than 32."));
            }

            if (!await _secrets.ExistsAsync(ownerId, secretName, cancellationToken).ConfigureAwait(false))
            {
                return CloudOutcome<List<string>>.Fail(CloudProblem.BadRequest("unknown_secret", "Unknown secret.", $"The secret '{secretName}' does not exist for this caller."));
            }

            refs.Add(secretName);
        }

        return refs.Count > 0 && !options.SupportsSessionVariables
            ? CloudOutcome<List<string>>.Fail(CloudProblem.BadRequest("secrets_unsupported", "Secrets are not supported here.", "The configured worker provider cannot give a worker its own environment variables, so a secret cannot be delivered."))
            : CloudOutcome<List<string>>.Ok(refs);
    }

    /// <summary>Hashes a normalized request so the same Idempotency-Key with a different request is told apart.</summary>
    /// <param name="request">The normalized request.</param>
    /// <returns>The SHA-256 of its JSON, in lower-case hexadecimal.</returns>
    public static string Fingerprint(CreateCloudAgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = JsonSerializer.Serialize(request, CloudJsonContext.Default.CreateCloudAgentRequest);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static bool IsBadHint(string? value) =>
        value is not null && (value.Length > MaximumHintLength || value.Any(char.IsControl));

    private static CloudOutcome<CreateCloudAgentRequest> Refuse(string code, string title, string detail) =>
        CloudOutcome<CreateCloudAgentRequest>.Fail(CloudProblem.BadRequest(code, title, detail));
}
