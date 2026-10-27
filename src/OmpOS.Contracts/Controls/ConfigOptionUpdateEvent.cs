namespace OmpOS.Contracts.Controls;

/// <summary>The data of a <c>config_option_update</c> stream event: an option of the session changed.</summary>
public sealed record ConfigOptionUpdateEvent
{
    /// <summary>Gets the identifier of the option that changed.</summary>
    public required string OptionId { get; init; }

    /// <summary>Gets the value the option has now.</summary>
    public required string CurrentValue { get; init; }
}
