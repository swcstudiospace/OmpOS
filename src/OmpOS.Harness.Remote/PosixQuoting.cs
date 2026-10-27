namespace OmpOS.Harness.Remote;

/// <summary>
/// Quotes values for a POSIX shell that parses a command line the remote side receives from ssh. Every dynamic value that ends up in
/// a remote command is passed through here, so a path or argument is always one word and never a command.
/// </summary>
public static class PosixQuoting
{
    /// <summary>
    /// Quotes a value as one single-quoted shell word: the text is wrapped in single quotes and every single quote in it becomes
    /// <c>'\''</c>. Nothing inside single quotes is expanded, so <c>$()</c>, backticks, <c>$VAR</c>, <c>;</c> and spaces are inert.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The quoted word.</returns>
    /// <exception cref="ArgumentException">The value contains NUL, a carriage return or a line feed: a command line must be one line (the remote login shell may not be POSIX).</exception>
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal))
        {
            throw new ArgumentException("A value in a remote command line must not contain a line break.", nameof(value));
        }

        return QuoteValue(value);
    }

    /// <summary>
    /// Quotes a value that may span several lines, for the body of a file the remote shell sources (never for a command line).
    /// Line breaks are kept inside the single quotes.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The quoted word.</returns>
    /// <exception cref="ArgumentException">The value contains NUL.</exception>
    public static string QuoteValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("A value passed to a remote shell must not contain NUL.", nameof(value));
        }

        return string.Concat("'", value.Replace("'", "'\\''", StringComparison.Ordinal), "'");
    }
}
