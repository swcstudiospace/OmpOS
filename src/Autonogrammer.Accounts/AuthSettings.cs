namespace Autonogrammer.Accounts;

/// <summary>The public sign-in settings of the project: which external providers are enabled. A sign-in screen offers only those.</summary>
/// <param name="External">The enabled flag of each provider name the service reports.</param>
public sealed record AuthSettings(IReadOnlyDictionary<string, bool> External)
{
    /// <summary>Gets whether the project has a provider enabled. A provider the service does not report is not enabled.</summary>
    /// <param name="provider">The provider name, for example <c>google</c>.</param>
    /// <returns>True when the service reports the provider as enabled.</returns>
    public bool IsEnabled(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        return External.TryGetValue(provider, out bool enabled) && enabled;
    }
}
