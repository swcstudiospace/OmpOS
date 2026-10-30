namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// The character rules for names the automation model accepts: secret names, branch names and branch globs.
/// </summary>
internal static class AutomationNames
{
    /// <summary>Returns whether a secret name is 1 to 64 ASCII letters, digits and underscores that does not start with a digit.</summary>
    /// <param name="name">The candidate.</param>
    /// <returns>True when the name is usable.</returns>
    public static bool IsSecretName(string? name)
    {
        if (name is null || name.Length is 0 or > 64 || char.IsAsciiDigit(name[0]))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a branch name or a branch glob has only printable characters and no space, backslash, <c>..</c> or leading dash.</summary>
    /// <param name="name">The candidate.</param>
    /// <returns>True when the text is acceptable.</returns>
    public static bool IsBranchText(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > AutomationLimits.MaxEntryLength || name[0] is '-' or '/')
        {
            return false;
        }

        if (name.Contains("..", StringComparison.Ordinal) || name.EndsWith('/') || name.EndsWith(".lock", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (char.IsControl(character) || character is ' ' or '\\' or '~' or '^' or ':')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a filter entry (an event or action name) is 1 to 100 ASCII letters, digits and underscores.</summary>
    /// <param name="name">The candidate.</param>
    /// <returns>True when the entry is acceptable.</returns>
    public static bool IsFilterWord(string? name)
    {
        if (name is null || name.Length is 0 or > 100)
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a short hint (environment id, model, mode) is printable and at most 100 characters.</summary>
    /// <param name="text">The candidate.</param>
    /// <returns>True when the hint is acceptable.</returns>
    public static bool IsHint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 100)
        {
            return false;
        }

        foreach (var character in text)
        {
            if (char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }
}
