using System.Text.Json.Serialization;

namespace OmpOS.Harness.Completion;

/// <summary>The body posted to the provider for an inline edit: the selected range's text and the instruction, nothing else.</summary>
/// <param name="Selection">The text of the selected range.</param>
/// <param name="Instruction">What the user asked to do to the selection.</param>
internal sealed record InlineEditRequestBody(
    [property: JsonPropertyName("selection")] string Selection,
    [property: JsonPropertyName("instruction")] string Instruction);
