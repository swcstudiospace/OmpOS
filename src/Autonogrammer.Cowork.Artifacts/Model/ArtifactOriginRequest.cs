namespace Autonogrammer.Cowork.Artifacts.Model;

/// <summary>One bounded network request an agent declared for a single artifact version.</summary>
/// <remarks>A request is never a grant: storing it grants nothing. The origin must already have passed
/// <c>ArtifactOriginRules.IsAllowed</c> at the tool boundary; this type only bounds shape and length.</remarks>
public sealed record ArtifactOriginRequest
{
    /// <summary>The maximum reason length, in characters.</summary>
    public const int MaxReasonLength = 280;

    /// <summary>The maximum origin length, in characters.</summary>
    public const int MaxOriginLength = 2048;

    /// <summary>Initializes a new instance of the <see cref="ArtifactOriginRequest"/> class.</summary>
    /// <param name="origin">The requested HTTPS origin. Must be non-empty.</param>
    /// <param name="reason">The agent-written reason. May be empty for a bare-string origin, but never longer than <see cref="MaxReasonLength"/> characters.</param>
    public ArtifactOriginRequest(string origin, string reason)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(reason);
        if (origin.Length == 0 || origin.Length > MaxOriginLength)
        {
            throw new ArgumentException("An origin request needs a non-empty origin of at most 2048 characters.", nameof(origin));
        }

        if (reason.Length > MaxReasonLength)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), "A reason is at most 280 characters.");
        }

        Origin = origin;
        Reason = reason;
    }

    /// <summary>Gets the requested HTTPS origin.</summary>
    public string Origin { get; init; }

    /// <summary>Gets the agent-written reason. Empty when the tool call gave a bare-string origin.</summary>
    public string Reason { get; init; }
}
