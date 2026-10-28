using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>The body of <c>PUT /models/default</c>.</summary>
public sealed record SetDefaultModelRequest
{
    /// <summary>Gets the model as <c>provider/model</c>. Required.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }
}
