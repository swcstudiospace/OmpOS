using System.Text;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// Shortens remote command output for an error message: the first 200 characters, with control characters replaced by spaces, so a
/// message never carries a whole transcript, a binary blob or a line break that would hide the rest of a log.
/// </summary>
public static class RemoteOutputExcerpt
{
    /// <summary>The most characters an excerpt keeps.</summary>
    public const int MaxCharacters = 200;

    /// <summary>Returns the excerpt of a text.</summary>
    /// <param name="text">The output, or null.</param>
    /// <returns>At most 200 characters, trimmed; the empty string when there is nothing to show.</returns>
    public static string Of(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(text.Length, MaxCharacters));
        foreach (var character in text)
        {
            if (builder.Length == MaxCharacters)
            {
                break;
            }

            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.ToString().Trim();
    }
}
