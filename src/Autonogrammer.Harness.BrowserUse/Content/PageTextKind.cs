namespace Autonogrammer.Harness.BrowserUse.Content;

/// <summary>Where page text came from.</summary>
public enum PageTextKind
{
    /// <summary>Visible text.</summary>
    Text = 0,

    /// <summary>An attribute such as alt, title or aria-label.</summary>
    Attribute = 1,

    /// <summary>An HTML comment.</summary>
    Comment = 2,

    /// <summary>A meta element.</summary>
    Meta = 3,

    /// <summary>A noscript element.</summary>
    Noscript = 4,
}
