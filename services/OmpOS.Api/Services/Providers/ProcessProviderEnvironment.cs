namespace OmpOS.Api.Services.Providers;

/// <summary>The environment of the service process.</summary>
internal sealed class ProcessProviderEnvironment : IProviderEnvironment
{
    /// <inheritdoc />
    public bool IsSet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name));
    }
}
