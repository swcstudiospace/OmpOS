namespace Autonogrammer.Contracts.Providers;

/// <summary>The answer of <c>PUT /models/default</c>.</summary>
public sealed record DefaultModelResponse
{
    /// <summary>Gets the default model as <c>provider/model</c>.</summary>
    public required string Model { get; init; }
}
