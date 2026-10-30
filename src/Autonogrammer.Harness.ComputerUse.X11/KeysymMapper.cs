namespace Autonogrammer.Harness.ComputerUse.X11;

/// <summary>
/// Maps a character to an X keysym. ASCII maps to the Latin-1 keysym (the code point).
/// Other characters map to the Unicode keysym <c>0x01000000</c> OR the code point.
/// A keysym with no keycode takes the scratch-keycode path and must be restored after <c>XSync</c>.
/// </summary>
public static class KeysymMapper
{
    /// <summary>Unicode keysym plane.</summary>
    public const uint UnicodePlane = 0x01000000;

    /// <summary>Maps <paramref name="value"/> to its keysym. Does not consult the server.</summary>
    /// <param name="value">The character.</param>
    /// <returns>The keysym.</returns>
    public static uint ToKeysym(char value)
    {
        if (value <= 0x7F)
        {
            return value;
        }

        return UnicodePlane | value;
    }

    /// <summary>
    /// Plans injection. <paramref name="hasKeycode"/> is the server lookup. A miss records the scratch path
    /// and that the previous mapping is restored after <c>XSync</c>.
    /// </summary>
    /// <param name="value">The character.</param>
    /// <param name="hasKeycode">Returns whether the keysym already has a keycode.</param>
    /// <returns>The plan.</returns>
    public static KeysymPlan Plan(char value, Func<uint, bool> hasKeycode)
    {
        ArgumentNullException.ThrowIfNull(hasKeycode);
        uint keysym = ToKeysym(value);
        bool scratch = !hasKeycode(keysym);
        return new KeysymPlan(keysym, scratch, scratch);
    }
}
