namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// Single-use ticket bound to an action hash and valid for <see cref="Lifetime"/> (2 seconds).
/// The executor consumes it; the gate does not inject.
/// </summary>
public sealed class ComputerActionTicket
{
    private readonly byte[] _actionHash;
    private int _consumed;

    /// <summary>How long a ticket remains valid after <see cref="IssuedAt"/>.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(2);

    /// <summary>Creates a ticket. <paramref name="issuedAt"/> comes from an injected <see cref="TimeProvider"/>; this type does not read the clock.</summary>
    /// <param name="actionHash">Action hash the ticket is bound to.</param>
    /// <param name="issuedAt">Issue time supplied by the caller.</param>
    public ComputerActionTicket(ReadOnlyMemory<byte> actionHash, DateTimeOffset issuedAt)
    {
        _actionHash = actionHash.ToArray();
        IssuedAt = issuedAt;
    }

    /// <summary>Issue time supplied by the gate.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>Hash this ticket is bound to.</summary>
    public ReadOnlyMemory<byte> ActionHash => _actionHash;

    /// <summary><see langword="true"/> when <paramref name="now"/> is within <see cref="Lifetime"/> of <see cref="IssuedAt"/>.</summary>
    /// <param name="now">Time from an injected <see cref="TimeProvider"/>.</param>
    /// <returns><see langword="true"/> when the ticket is still inside its 2 second window.</returns>
    public bool IsWithinLifetime(DateTimeOffset now) => now >= IssuedAt && now <= IssuedAt + Lifetime;

    /// <summary><see langword="true"/> when <paramref name="actionHash"/> equals the bound hash.</summary>
    /// <param name="actionHash">Hash to compare.</param>
    /// <returns><see langword="true"/> on an equal hash.</returns>
    public bool Matches(ReadOnlyMemory<byte> actionHash) => actionHash.Span.SequenceEqual(_actionHash);

    /// <summary>Consumes the ticket once. A second call returns <see langword="false"/>.</summary>
    /// <returns><see langword="true"/> only for the first consumer.</returns>
    public bool TryConsume() => Interlocked.Exchange(ref _consumed, 1) == 0;
}

