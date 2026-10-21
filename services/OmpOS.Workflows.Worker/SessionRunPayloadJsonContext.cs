using System.Text.Json.Serialization;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Contracts.Workflows;

namespace OmpOS.Workflows.Worker;

/// <summary>
/// Source-generated JSON metadata for the Temporal payload types.
/// </summary>
/// <remarks>
/// The worker and the replay tests run with reflection-based serialization disabled
/// (trim/AOT safety), while Temporal's default payload converter relies on reflection.
/// <see cref="SessionRunDataConverter"/> plugs this context into a
/// <c>DefaultPayloadConverter</c> so workflow inputs, activity arguments, and results
/// serialize without reflection. Concrete collection types are registered because payloads
/// arrive at runtime as <c>string[]</c>, <c>List&lt;string&gt;</c>, and
/// <c>Dictionary&lt;string, string&gt;</c> behind interface-typed properties.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SessionRunInput))]
[JsonSerializable(typeof(SessionRunResult))]
[JsonSerializable(typeof(SandboxDescriptor))]
[JsonSerializable(typeof(WorkflowRunStatus))]
[JsonSerializable(typeof(Isolation))]
[JsonSerializable(typeof(Attestation))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class SessionRunPayloadJsonContext : JsonSerializerContext
{
}
