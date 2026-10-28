namespace OmpOS.Api.Cloud;

/// <summary>Resolves secret names to values through the registered <see cref="ICloudSecretProvider"/>s, in registration order: an owner's own secret wins over an operator-defined one of the same name.</summary>
internal sealed class CloudSecretResolver
{
    private readonly IReadOnlyList<ICloudSecretProvider> _providers;

    /// <summary>Initializes a new instance of the <see cref="CloudSecretResolver"/> class.</summary>
    /// <param name="providers">The providers, in the order they are asked.</param>
    public CloudSecretResolver(IEnumerable<ICloudSecretProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToList();
    }

    /// <summary>Resolves the values of <paramref name="names"/> for an owner.</summary>
    /// <param name="ownerId">The owner of the agent.</param>
    /// <param name="names">The secret names the agent references.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The values found and the names that no provider had.</returns>
    public async Task<CloudSecretResolution> ResolveAsync(string ownerId, IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(names);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var name in names.Distinct(StringComparer.Ordinal))
        {
            string? found = null;
            foreach (var provider in _providers)
            {
                found = await provider.TryGetValueAsync(ownerId, name, cancellationToken).ConfigureAwait(false);
                if (found is not null)
                {
                    break;
                }
            }

            if (found is null)
            {
                missing.Add(name);
            }
            else
            {
                values[name] = found;
            }
        }

        return new CloudSecretResolution(values, missing);
    }

    /// <summary>Returns whether any provider has a secret of that name for the owner.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="name">The secret name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a provider has it.</returns>
    public async Task<bool> ExistsAsync(string ownerId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(name);
        foreach (var provider in _providers)
        {
            if (await provider.ExistsAsync(ownerId, name, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }
}
