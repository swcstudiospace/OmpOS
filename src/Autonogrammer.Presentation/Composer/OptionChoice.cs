namespace Autonogrammer.Presentation.Composer;

/// <summary>One value of a session option the engine offers, as an option chip lists it, for example <c>plan</c> for the mode.</summary>
/// <param name="Value">The engine's identifier of the value. The chip sends it back unchanged to select the value.</param>
/// <param name="Name">The display name of the value. A blank name falls back to the identifier.</param>
/// <param name="Description">The plain-language description the engine gave the value, or <see langword="null"/>.</param>
public sealed record OptionChoice(string Value, string Name, string? Description)
{
    /// <summary>Creates a choice. A blank name falls back to the identifier and a blank description to <see langword="null"/>.</summary>
    /// <param name="value">The engine's identifier of the value.</param>
    /// <param name="name">The display name.</param>
    /// <param name="description">The description, or null.</param>
    /// <returns>The choice.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty or white space.</exception>
    public static OptionChoice Create(string value, string? name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new OptionChoice(value, string.IsNullOrWhiteSpace(name) ? value : name, string.IsNullOrWhiteSpace(description) ? null : description);
    }
}
