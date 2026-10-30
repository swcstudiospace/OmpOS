using System.Text.Json;

namespace Autonogrammer.Cowork.Abstractions;

/// <summary>A tool a provider offers the model.</summary>
public sealed record CoworkToolDescriptor
{
    /// <summary>Gets the tool name. It starts with the provider id and an underscore, and never contains a double underscore.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the provider that offers the tool.</summary>
    public required string ProviderId { get; init; }

    /// <summary>Gets the model-facing description, at most 1024 characters.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the JSON Schema of the arguments. The schema type is object.</summary>
    public required JsonElement InputSchema { get; init; }

    /// <summary>Gets what the tool does outside the model.</summary>
    public required CoworkToolEffect Effect { get; init; }

    /// <summary>Gets who decides whether a call may run.</summary>
    public required CoworkApprovalRequirement Approval { get; init; }

    /// <summary>Gets the kinds of content the tool returns. The default is text.</summary>
    public CoworkResultKinds Results { get; init; } = CoworkResultKinds.Text;
}
