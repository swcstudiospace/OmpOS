using System.Globalization;

namespace Autonogrammer.Presentation.Composer;

/// <summary>Every user-facing sentence of the context ring.</summary>
public static class ContextRingCopy
{
    /// <summary>The accessible name of the ring.</summary>
    public const string Name = "Context window";

    /// <summary>The state word of <see cref="ContextRingState.Normal"/>.</summary>
    public const string StateNormal = "Plenty of room";

    /// <summary>The state word of <see cref="ContextRingState.Warning"/>.</summary>
    public const string StateWarning = "Getting full";

    /// <summary>The state word of <see cref="ContextRingState.Critical"/>.</summary>
    public const string StateCritical = "Almost full";

    /// <summary>The state word when the window size is not known.</summary>
    public const string StateUnknown = "Window size unknown";

    /// <summary>The text of the ring before the engine has reported any usage.</summary>
    public const string NoUsageYet = "Context usage appears after the first reply.";

    /// <summary>Returns the state word of a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The words.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is not defined.</exception>
    public static string StateWord(ContextRingState state) => state switch
    {
        ContextRingState.Unknown => StateUnknown,
        ContextRingState.Normal => StateNormal,
        ContextRingState.Warning => StateWarning,
        ContextRingState.Critical => StateCritical,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown context ring state."),
    };

    /// <summary>Formats a token count compactly.</summary>
    /// <param name="tokens">The count.</param>
    /// <returns>For example <c>950</c>, <c>12.3k</c> or <c>1.2M</c>.</returns>
    public static string FormatTokens(long tokens) => tokens switch
    {
        < 1000 => tokens.ToString(CultureInfo.InvariantCulture),
        < 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{tokens / 1000.0:0.#}k"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{tokens / 1_000_000.0:0.#}M"),
    };
}
