using Autonogrammer.Contracts.Sessions;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// Maps <see cref="SessionSurface"/> to the text of the <c>sessions.surface</c> column and back. The column holds the lower-case name (<c>code</c>, <c>cowork</c>)
/// and has no CHECK constraint, so a new surface is additive; reading a value this build does not know fails loudly instead of guessing a surface.
/// </summary>
internal static class SessionSurfaceColumn
{
    /// <summary>Gets the column text of a surface.</summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The lower-case name stored in the <c>surface</c> column.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The surface is not a defined value.</exception>
    internal static string ToColumn(SessionSurface surface) => surface switch
    {
        SessionSurface.Code => "code",
        SessionSurface.Cowork => "cowork",
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "The session surface is not a defined value."),
    };

    /// <summary>Gets the surface of a column text.</summary>
    /// <param name="value">The text read from the <c>surface</c> column.</param>
    /// <returns>The surface.</returns>
    /// <exception cref="InvalidOperationException">The text names no surface this build knows.</exception>
    internal static SessionSurface FromColumn(string value) => value switch
    {
        "code" => SessionSurface.Code,
        "cowork" => SessionSurface.Cowork,
        _ => throw new InvalidOperationException($"A stored session has the unknown surface '{value}'."),
    };
}
