using System.Globalization;

namespace OmpOS.Presentation.Cloud;

/// <summary>
/// The rules for what a user may type into the new-agent dialog. Every method returns an error sentence, or null when the entry is fine; no
/// sentence repeats the entry, so a pasted secret never comes back onto the screen.
/// </summary>
public static class CloudInputValidator
{
    /// <summary>The most characters of an agent name.</summary>
    public const int MaxNameLength = 80;

    /// <summary>The most characters of a repository address.</summary>
    public const int MaxRepositoryUrlLength = 2048;

    /// <summary>The most repositories one agent can work on.</summary>
    public const int MaxRepositories = 8;

    /// <summary>The most characters of a branch name.</summary>
    public const int MaxBranchLength = 200;

    /// <summary>The most characters of a secret name.</summary>
    public const int MaxSecretNameLength = 64;

    /// <summary>The most secret names one agent can use.</summary>
    public const int MaxSecretNames = 20;

    /// <summary>The most characters of a first task.</summary>
    public const int MaxPromptLength = 8000;

    /// <summary>The smallest idle time, in minutes.</summary>
    public const int MinIdleMinutes = 1;

    private const string GitSuffix = ".git";
    private const string LockSuffix = ".lock";
    private const string ForbiddenBranchCharacters = "~^:?*[\\";

    /// <summary>Checks an agent name: one to <see cref="MaxNameLength"/> characters with no control character.</summary>
    /// <param name="name">The entry.</param>
    /// <returns>The error, or null.</returns>
    public static string? ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return CloudCopy.NameRequired;
        }

        if (trimmed.Length > MaxNameLength)
        {
            return CloudCopy.NameTooLong(MaxNameLength);
        }

        return trimmed.Any(char.IsControl) ? CloudCopy.NameInvalid : null;
    }

    /// <summary>
    /// Checks a repository address and normalises it. An address must be https, name a host and an owner and a repository, and carry no
    /// user name, password, token, query or fragment.
    /// </summary>
    /// <param name="text">The entry.</param>
    /// <param name="repository">The normalised repository when the entry is valid; otherwise null.</param>
    /// <returns>The error, or null.</returns>
    public static string? ValidateRepositoryAddress(string? text, out CloudRepository? repository)
    {
        repository = null;
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return CloudCopy.RepositoryRequired;
        }

        if (trimmed.Length > MaxRepositoryUrlLength)
        {
            return CloudCopy.RepositoryTooLong;
        }

        if (trimmed.Any(static character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            return CloudCopy.RepositoryInvalid;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return CloudCopy.RepositoryNotHttps;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return CloudCopy.RepositoryNotHttps;
        }

        if (uri.UserInfo.Length > 0)
        {
            return CloudCopy.RepositoryHasCredentials;
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return CloudCopy.RepositoryHasQuery;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (uri.Host.Length == 0 || segments.Length < 2)
        {
            return CloudCopy.RepositoryNeedsOwnerAndName;
        }

        var path = string.Join('/', segments);
        var authority = uri.IsDefaultPort ? uri.Host : string.Create(CultureInfo.InvariantCulture, $"{uri.Host}:{uri.Port}");
        repository = new CloudRepository(string.Create(CultureInfo.InvariantCulture, $"https://{authority}/{path}"));
        return null;
    }

    /// <summary>Gets the key that tells two repository addresses apart: the host and path, lower case, without a trailing <c>.git</c>.</summary>
    /// <param name="address">A normalised repository address.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var withoutGit = address.EndsWith(GitSuffix, StringComparison.OrdinalIgnoreCase) ? address[..^GitSuffix.Length] : address;
        return withoutGit.ToUpperInvariant();
    }

    /// <summary>Gets <c>owner/repository</c> from a normalised address, without a trailing <c>.git</c>. An address that cannot be read is returned as it is.</summary>
    /// <param name="address">A normalised repository address.</param>
    /// <returns>The short name.</returns>
    public static string DisplayNameOf(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return address;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return address;
        }

        var name = segments[^1];
        if (name.EndsWith(GitSuffix, StringComparison.OrdinalIgnoreCase) && name.Length > GitSuffix.Length)
        {
            name = name[..^GitSuffix.Length];
        }

        return string.Concat(segments[^2], "/", name);
    }

    /// <summary>
    /// Checks a branch name against the git reference rules that matter here. A blank entry is valid: the host picks the branch or uses the
    /// repository's default.
    /// </summary>
    /// <param name="branch">The entry.</param>
    /// <returns>The error, or null.</returns>
    public static string? ValidateBranch(string? branch)
    {
        var trimmed = branch?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > MaxBranchLength)
        {
            return CloudCopy.BranchTooLong(MaxBranchLength);
        }

        var invalid = trimmed.Any(static character => char.IsControl(character) || char.IsWhiteSpace(character) || ForbiddenBranchCharacters.Contains(character, StringComparison.Ordinal))
            || trimmed.Contains("..", StringComparison.Ordinal)
            || trimmed.Contains("@{", StringComparison.Ordinal)
            || trimmed.Contains("//", StringComparison.Ordinal)
            || string.Equals(trimmed, "@", StringComparison.Ordinal)
            || trimmed.StartsWith('-')
            || trimmed.StartsWith('/')
            || trimmed.EndsWith('/')
            || trimmed.EndsWith('.')
            || trimmed.Split('/').Any(static part => part.StartsWith('.') || part.EndsWith(LockSuffix, StringComparison.Ordinal));
        return invalid ? CloudCopy.BranchInvalid : null;
    }

    /// <summary>
    /// Checks a secret name: letters, digits and underscores, starting with a letter or an underscore. Anything else, including an equals
    /// sign, is refused with a sentence that tells the user to enter the name only.
    /// </summary>
    /// <param name="name">The entry.</param>
    /// <returns>The error, or null.</returns>
    public static string? ValidateSecretName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return CloudCopy.SecretNameRequired;
        }

        if (trimmed.Length > MaxSecretNameLength)
        {
            return CloudCopy.SecretNameTooLong(MaxSecretNameLength);
        }

        var first = trimmed[0];
        var startsWell = first == '_' || char.IsAsciiLetter(first);
        var allowed = trimmed.All(static character => character == '_' || char.IsAsciiLetterOrDigit(character));
        return startsWell && allowed ? null : CloudCopy.SecretNameInvalid;
    }

    /// <summary>Checks the idle time typed as text: a whole number of minutes from <see cref="MinIdleMinutes"/> to <paramref name="maxMinutes"/>.</summary>
    /// <param name="text">The entry.</param>
    /// <param name="maxMinutes">The largest idle time the service accepts.</param>
    /// <param name="minutes">The parsed minutes when the entry is valid; otherwise 0.</param>
    /// <returns>The error, or null.</returns>
    public static string? ValidateIdleMinutes(string? text, int maxMinutes, out int minutes)
    {
        minutes = 0;
        var trimmed = text?.Trim() ?? string.Empty;
        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            || parsed < MinIdleMinutes
            || parsed > maxMinutes)
        {
            return CloudCopy.IdleInvalid(MinIdleMinutes, maxMinutes);
        }

        minutes = parsed;
        return null;
    }

    /// <summary>Checks the optional first task: at most <see cref="MaxPromptLength"/> characters.</summary>
    /// <param name="prompt">The entry.</param>
    /// <returns>The error, or null.</returns>
    public static string? ValidatePrompt(string? prompt) =>
        prompt is not null && prompt.Length > MaxPromptLength ? CloudCopy.PromptTooLong(MaxPromptLength) : null;
}
