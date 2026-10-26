namespace OmpOS.Harness.Herdr;

/// <summary>
/// The identifier of one herdr session. herdr keeps each named session in a directory of the same name, so the value is
/// restricted to a safe path segment: ASCII letters and digits, with dots, underscores and hyphens allowed after the first
/// character.
/// </summary>
public sealed record HerdrSessionId
{
    /// <summary>The longest accepted identifier, in characters.</summary>
    public const int MaxLength = 64;

    /// <summary>Creates a validated session identifier.</summary>
    /// <param name="value">The identifier text.</param>
    /// <exception cref="ArgumentException">The value is not a valid identifier.</exception>
    public HerdrSessionId(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(
                "A herdr session id is 1 to 64 ASCII letters, digits, dots, underscores or hyphens, and starts with a letter or digit.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns true when the text is a valid session identifier.</summary>
    /// <param name="value">The text to check.</param>
    public static bool IsValid(string? value)
    {
        if (value is null || value.Length is 0 or > MaxLength || !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
