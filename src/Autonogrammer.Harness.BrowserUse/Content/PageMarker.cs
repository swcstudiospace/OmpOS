using System.Security.Cryptography;

namespace Autonogrammer.Harness.BrowserUse.Content;

/// <summary>Wraps page text in a fresh random-id envelope and neutralises delimiter look-alikes.</summary>
public sealed class PageMarker
{
    /// <summary>Initializes a marker with a fresh id.</summary>
    public PageMarker()
        : this(Convert.ToHexString(RandomNumberGenerator.GetBytes(8)))
    {
    }

    /// <summary>Initializes a marker with a known id. Tests use this.</summary>
    /// <param name="id">The envelope id.</param>
    public PageMarker(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
    }

    /// <summary>Gets the envelope id.</summary>
    public string Id { get; }

    /// <summary>Neutralises angle brackets and forged system-message labels, then wraps the text.</summary>
    /// <param name="text">The page text.</param>
    /// <returns>The enveloped text.</returns>
    public string Wrap(string text)
    {
        var neutral = Neutralise(text ?? string.Empty);
        return "[page content begins, id " + Id + "]\n" + neutral + "\n[page content ends, id " + Id + "]";
    }

    /// <summary>Names the origin on an image description.</summary>
    /// <param name="origin">The origin.</param>
    /// <returns>The description.</returns>
    public static string ImageDescription(string origin) =>
        "Untrusted page image from " + (string.IsNullOrEmpty(origin) ? "unknown origin" : origin);

    /// <summary>Replaces angle-bracket look-alikes and forged system labels.</summary>
    /// <param name="text">The raw text.</param>
    /// <returns>The neutral text.</returns>
    public static string Neutralise(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text
            .Replace("<", "\u2039", StringComparison.Ordinal)
            .Replace(">", "\u203a", StringComparison.Ordinal)
            .Replace("system message", "page label", StringComparison.OrdinalIgnoreCase)
            .Replace("[page content ends", "[page label", StringComparison.OrdinalIgnoreCase);
    }
}
