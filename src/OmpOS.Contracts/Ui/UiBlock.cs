using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Ui;

/// <summary>
/// Base record for every Generative UI block. Deserialization never throws on
/// unknown or missing <c>kind</c> discriminators: they degrade to
/// <see cref="UnknownBlock"/> with the raw payload preserved.
/// </summary>
[JsonConverter(typeof(UiBlockConverter))]
public abstract record UiBlock
{
    /// <summary>Gets the block identifier.</summary>
    public required string Id { get; init; }
}
