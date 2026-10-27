using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Hook matcher semantics: a .NET regular expression tested with <see cref="Regex.IsMatch(string)"/> (unanchored), so
/// Cursor's own examples <c>curl|wget|nc </c> and <c>explore|shell</c> work. An empty matcher or <c>*</c> matches every
/// call. A regular expression that does not compile never matches.
/// </summary>
public static class HookMatcher
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// True when the matcher is absent, empty or <c>*</c>.
    /// </summary>
    /// <param name="matcher">The matcher text from the hook file.</param>
    public static bool MatchesEverything(string? matcher) =>
        string.IsNullOrWhiteSpace(matcher) || string.Equals(matcher.Trim(), "*", StringComparison.Ordinal);

    /// <summary>
    /// Compiles a matcher. A match-everything matcher compiles to null with no problem.
    /// </summary>
    /// <param name="matcher">The matcher text.</param>
    /// <param name="regex">The compiled expression, or null for a match-everything matcher or an invalid one.</param>
    /// <param name="problem">Why the matcher is invalid, or null.</param>
    /// <returns>False when the matcher is not a valid regular expression.</returns>
    public static bool TryCompile(string? matcher, out Regex? regex, [NotNullWhen(false)] out string? problem)
    {
        regex = null;
        problem = null;
        if (MatchesEverything(matcher))
        {
            return true;
        }

        try
        {
            regex = new Regex(matcher!, RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, MatchTimeout);
            return true;
        }
        catch (ArgumentException ex)
        {
            problem = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Tests a call against a hook's matcher.
    /// </summary>
    /// <param name="matcher">The matcher text from the hook file.</param>
    /// <param name="compiled">The expression from <see cref="TryCompile"/>; null for a match-everything matcher.</param>
    /// <param name="value">The value of the call, or null when the event has none.</param>
    /// <returns>True when the hook applies to the call.</returns>
    public static bool Matches(string? matcher, Regex? compiled, string? value)
    {
        if (MatchesEverything(matcher))
        {
            return true;
        }

        if (compiled is null || value is null)
        {
            return false;
        }

        try
        {
            return compiled.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
