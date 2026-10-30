namespace Autonogrammer.Harness.BrowserUse;

/// <summary>A session host bound was exceeded. The message names the bound, not a measured value.</summary>
public sealed class BrowserSessionLimitException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="BrowserSessionLimitException"/> class.</summary>
    public BrowserSessionLimitException()
        : this(BrowserSessionLimit.ConcurrentBrowsers)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BrowserSessionLimitException"/> class.</summary>
    /// <param name="message">The message. The limit is recorded as concurrent browsers.</param>
    public BrowserSessionLimitException(string message)
        : base(message)
    {
        Limit = BrowserSessionLimit.ConcurrentBrowsers;
    }

    /// <summary>Initializes a new instance of the <see cref="BrowserSessionLimitException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public BrowserSessionLimitException(string message, Exception innerException)
        : base(message, innerException)
    {
        Limit = BrowserSessionLimit.ConcurrentBrowsers;
    }

    /// <summary>Initializes the exception.</summary>
    /// <param name="limit">The bound that was exceeded.</param>
    public BrowserSessionLimitException(BrowserSessionLimit limit)
        : base("Browser session bound " + limit.ToString() + " was exceeded.")
    {
        Limit = limit;
    }

    /// <summary>Gets the bound that was exceeded.</summary>
    public BrowserSessionLimit Limit { get; }
}
