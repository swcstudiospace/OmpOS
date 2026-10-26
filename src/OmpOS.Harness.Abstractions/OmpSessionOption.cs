namespace OmpOS.Harness.Abstractions;

/// <summary>One select-style option of a session that the engine owns, for example its mode or its thinking level.</summary>
/// <param name="Id">The engine's identifier of the option, for example <c>mode</c>. It is sent back verbatim to change the option.</param>
/// <param name="Name">The display name the engine gave the option, or <see langword="null"/>.</param>
/// <param name="Category">The category the engine gave the option (<c>mode</c>, <c>model</c>, <c>thought_level</c>), or <see langword="null"/>.</param>
/// <param name="CurrentValue">The value the option has now.</param>
/// <param name="Values">The values the option can take, in the engine's order.</param>
public sealed record OmpSessionOption(string Id, string? Name, string? Category, string CurrentValue, IReadOnlyList<OmpSessionOptionValue> Values)
{
    /// <summary>Determines whether <paramref name="other"/> describes the same option with the same current value and the same values.</summary>
    /// <param name="other">The option to compare with.</param>
    /// <returns><see langword="true"/> when nothing differs.</returns>
    public bool IsSameAs(OmpSessionOption? other) =>
        other is not null
        && string.Equals(Id, other.Id, StringComparison.Ordinal)
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && string.Equals(Category, other.Category, StringComparison.Ordinal)
        && string.Equals(CurrentValue, other.CurrentValue, StringComparison.Ordinal)
        && Values.SequenceEqual(other.Values);
}
