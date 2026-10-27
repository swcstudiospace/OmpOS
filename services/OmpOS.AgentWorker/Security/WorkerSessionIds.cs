namespace OmpOS.AgentWorker.Security;

/// <summary>
/// The shape of a session subject. The subject names the session's working directory on the volume, so it must be a single safe
/// path segment: it never contains a separator or a leading dot, and it is short.
/// </summary>
internal static class WorkerSessionIds
{
    /// <summary>Gets the longest accepted session subject, in characters.</summary>
    public const int MaxLength = 64;

    /// <summary>Returns whether <paramref name="value"/> is a safe session subject: 1 to 64 ASCII letters, digits, <c>.</c>, <c>_</c> or <c>-</c>, starting with a letter or digit.</summary>
    /// <param name="value">The candidate subject.</param>
    /// <returns><see langword="true"/> when the value may name a session directory.</returns>
    public static bool IsSafe(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength || !char.IsAsciiLetterOrDigit(value[0]))
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
}
