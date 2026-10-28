namespace Autonogrammer.Contracts.Ui;

/// <summary>
/// An error block (kind "error").
/// </summary>
public sealed record ErrorBlock : UiBlock
{
    /// <summary>Gets the error message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets whether the failed operation may be retried.</summary>
    public required bool Retryable { get; init; }
}
