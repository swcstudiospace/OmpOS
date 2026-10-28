namespace OmpOS.Contracts.Cloud;

/// <summary>Why a cloud agent is in <see cref="CloudAgentStatus.Failed"/> or <see cref="CloudAgentStatus.Orphaned"/>. The message never carries a secret value.</summary>
public sealed record CloudAgentError
{
    /// <summary>Gets the stable machine-readable code, for example <c>wake_timeout</c>.</summary>
    public required string Code { get; init; }

    /// <summary>Gets a short human-readable explanation.</summary>
    public required string Message { get; init; }
}
