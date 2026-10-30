namespace Autonogrammer.Contracts.Controls;

/// <summary>The body of <c>PUT /sessions/{id}/config-options/{optionId}</c>.</summary>
public sealed record SetConfigOptionRequest
{
    /// <summary>Gets the value to select. Required; one of the option's <see cref="ConfigOptionValue.Value"/> entries.</summary>
    public string? Value { get; init; }
}
