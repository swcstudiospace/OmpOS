namespace Autonogrammer.Harness.Rpc;

/// <summary>Failure detail of a <c>prompt_result</c> whose status is <c>error</c>.</summary>
public sealed record RpcPromptError
{
    /// <summary>Gets the provider's error text.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the provider that failed, when known.</summary>
    public string? Provider { get; init; }

    /// <summary>Gets the model that failed, when known.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the HTTP status the provider returned, when the failure came from a request.</summary>
    public int? HttpStatus { get; init; }

    /// <summary>Gets whether resubmitting later may succeed.</summary>
    public bool Retryable { get; init; }
}
