namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>
/// Removes a submitted API key from the message a source returned, so a failed save or setup never echoes the key into
/// <c>ErrorText</c>. Both provider card models use this one implementation, so a change to the scrub applies to every card.
/// </summary>
internal static class ProviderKeyScrubber
{
    private const string Hidden = "[hidden]";

    /// <summary>Replaces every occurrence of the key in the message with a fixed marker.</summary>
    /// <param name="message">The message a source returned. May be null or blank.</param>
    /// <param name="key">The key that was submitted. An empty key leaves the message unchanged.</param>
    /// <returns>The scrubbed message; null when the message is null or blank.</returns>
    internal static string? Hide(string? message, string key)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return key.Length == 0 ? message : message.Replace(key, Hidden, StringComparison.Ordinal);
    }
}
