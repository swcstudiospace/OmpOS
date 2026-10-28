using Microsoft.Extensions.Options;
using Autonogrammer.Api.Cloud.Options;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The operator-defined secrets of <c>Cloud:Secrets:Static</c>, available to every owner. They are read from configuration, so they come from the
/// host's secret store (environment variables, a mounted secret), never from a committed file. The API does not list or return them.
/// </summary>
internal sealed class OptionsCloudSecretProvider : ICloudSecretProvider
{
    private readonly IOptionsMonitor<CloudOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="OptionsCloudSecretProvider"/> class.</summary>
    /// <param name="options">The cloud options.</param>
    public OptionsCloudSecretProvider(IOptionsMonitor<CloudOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public ValueTask<string?> TryGetValueAsync(string ownerId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ValueTask.FromResult(_options.CurrentValue.Secrets.Static.GetValueOrDefault(name));
    }

    /// <inheritdoc />
    public ValueTask<bool> ExistsAsync(string ownerId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ValueTask.FromResult(_options.CurrentValue.Secrets.Static.ContainsKey(name));
    }
}
