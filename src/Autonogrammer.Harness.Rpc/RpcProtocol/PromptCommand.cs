namespace Autonogrammer.Harness.Rpc;

/// <summary>Submits a prompt. An admitted prompt is completed later by a <c>prompt_result</c> frame with the same id.</summary>
public sealed record PromptCommand : RpcCommand
{
    /// <inheritdoc />
    public override string Type => "prompt";

    /// <summary>Gets the prompt text.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the optional images attached to the prompt.</summary>
    public IReadOnlyList<RpcImageContent>? Images { get; init; }

    /// <summary>Gets how the prompt joins an in-flight turn: <c>steer</c> or <c>followUp</c>. Null queues it normally.</summary>
    public string? StreamingBehavior { get; init; }
}
