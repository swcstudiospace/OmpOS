using Microsoft.Extensions.Options;
using OmpOS.Api.Cloud.Options;
using OmpOS.Contracts.Cloud;

namespace OmpOS.Api.Cloud;

/// <summary>Builds the redactor of an agent: the values of the secrets it references, the git tokens currently cached, and the configured personal access token. Everything the worker holds in its environment that is a secret is a pattern the redactor removes from output.</summary>
internal sealed class CloudRedactorFactory
{
    private readonly CloudSecretResolver _secrets;
    private readonly InstallationTokenCache _tokens;
    private readonly IOptionsMonitor<CloudOptions> _options;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="CloudRedactorFactory"/> class.</summary>
    /// <param name="secrets">The secret resolver.</param>
    /// <param name="tokens">The installation token cache.</param>
    /// <param name="options">The cloud options (for the personal access token).</param>
    /// <param name="time">The clock.</param>
    public CloudRedactorFactory(CloudSecretResolver secrets, InstallationTokenCache tokens, IOptionsMonitor<CloudOptions> options, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        _secrets = secrets;
        _tokens = tokens;
        _options = options;
        _time = time;
    }

    /// <summary>Creates the redactor for an agent.</summary>
    /// <param name="agent">The agent's record.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The redactor.</returns>
    public async Task<CloudSecretRedactor> CreateAsync(CloudAgentRecord agent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var resolution = await _secrets.ResolveAsync(agent.OwnerId, agent.SecretRefs, cancellationToken).ConfigureAwait(false);
        var values = new List<string>(resolution.Values.Values);
        values.AddRange(_tokens.CurrentTokens(_time.GetUtcNow()));
        var pat = _options.CurrentValue.Git.Pat;
        if (!string.IsNullOrWhiteSpace(pat))
        {
            values.Add(pat);
        }

        return values.Count == 0 ? CloudSecretRedactor.Empty : new CloudSecretRedactor(values);
    }
}
