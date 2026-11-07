namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Hub to device challenge.</summary>
public sealed record DispatchChallengeFrame
{
    /// <summary>Protocol version. Always 1.</summary>
    public required int V { get; init; }

    /// <summary>Frame type <c>challenge</c>.</summary>
    public required string T { get; init; }

    /// <summary>128-bit nonce, base64url.</summary>
    public required string Nonce { get; init; }

    /// <summary>Hub clock, Unix seconds.</summary>
    public required long ServerTime { get; init; }

    /// <summary>Hub origin the desktop must bind.</summary>
    public required string Origin { get; init; }

    /// <summary>Supported protocol names.</summary>
    public required IReadOnlyList<string> Protocols { get; init; }
}
