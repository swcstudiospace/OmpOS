namespace Autonogrammer.Cowork.Tools;

/// <summary>Timeouts and the provider profile the host applies. Approval keys of the hosted API are not read.</summary>
public sealed class CoworkToolHostOptions
{
    /// <summary>Gets how long an ask may wait. The default is 120 seconds. A timeout is a denial the model reads as not approved.</summary>
    public TimeSpan ApprovalTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>Gets how long a provider call may run. The default is 120 seconds.</summary>
    public TimeSpan CallTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Gets the provider ids the profile admits. Null admits every registered provider.
    /// </summary>
    public IReadOnlySet<string>? AdmittedProviders { get; init; }
}

