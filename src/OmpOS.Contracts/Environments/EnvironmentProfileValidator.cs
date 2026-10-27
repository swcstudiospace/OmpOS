namespace OmpOS.Contracts.Environments;

/// <summary>
/// Checks profiles against <see cref="SshProfileRules"/>. A profile that passes is safe to turn into an ssh argument list and a
/// remote command; a profile that fails is never used. The messages name the property and the rule, never the rejected value.
/// </summary>
public static class EnvironmentProfileValidator
{
    private const int MaxDisplayNameLength = 100;

    /// <summary>Validates an environment profile.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The issues found, empty when the profile is acceptable.</returns>
    public static IReadOnlyList<ProfileIssue> Validate(EnvironmentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = new List<ProfileIssue>();
        if (!SshProfileRules.IsValidProfileId(profile.Id))
        {
            issues.Add(new ProfileIssue(nameof(EnvironmentProfile.Id), "The id must be 1 to 64 letters, digits, '.', '_' or '-' and start with a letter or digit."));
        }

        if (string.IsNullOrWhiteSpace(profile.DisplayName) || profile.DisplayName.Length > MaxDisplayNameLength || ContainsControl(profile.DisplayName))
        {
            issues.Add(new ProfileIssue(nameof(EnvironmentProfile.DisplayName), "The display name must be 1 to 100 characters without control characters."));
        }

        if (!Enum.IsDefined(profile.Kind))
        {
            issues.Add(new ProfileIssue(nameof(EnvironmentProfile.Kind), "The environment kind is not known."));
        }

        switch (profile.Kind)
        {
            case EnvironmentKind.Ssh when profile.Ssh is null:
                issues.Add(new ProfileIssue(nameof(EnvironmentProfile.Ssh), "An ssh environment needs its ssh details."));
                break;
            case EnvironmentKind.Ssh:
                issues.AddRange(Validate(profile.Ssh!));
                break;
            case EnvironmentKind.Local or EnvironmentKind.Cloud when profile.Ssh is not null:
                issues.Add(new ProfileIssue(nameof(EnvironmentProfile.Ssh), "Only an ssh environment carries ssh details."));
                break;
            default:
                break;
        }

        return issues;
    }

    /// <summary>Validates an ssh profile. Property names in the issues are prefixed with <c>Ssh.</c>.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The issues found, empty when the profile is acceptable.</returns>
    public static IReadOnlyList<ProfileIssue> Validate(SshProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = new List<ProfileIssue>();

        if (!SshProfileRules.IsValidAlias(profile.Alias))
        {
            issues.Add(Issue(nameof(SshProfile.Alias), "The alias must be 1 to 128 letters, digits, '.', '_', '@' or '-' and must not start with '-'."));
        }

        if (profile.Hostname is not null && !SshProfileRules.IsValidHostname(profile.Hostname))
        {
            issues.Add(Issue(nameof(SshProfile.Hostname), "The host name must be a DNS name or an IP address and must not start with '-'."));
        }

        if (profile.Port is { } port && !SshProfileRules.IsValidPort(port))
        {
            issues.Add(Issue(nameof(SshProfile.Port), "The port must be between 1 and 65535."));
        }

        if (profile.User is not null && !SshProfileRules.IsValidUser(profile.User))
        {
            issues.Add(Issue(nameof(SshProfile.User), "The user must be 1 to 64 letters, digits, '.', '_' or '-' and must not start with '-'."));
        }

        if (profile.IdentityFile is not null && !SshProfileRules.IsValidLocalPath(profile.IdentityFile))
        {
            issues.Add(Issue(nameof(SshProfile.IdentityFile), "The identity file must be a path without control characters that does not start with '-'."));
        }

        if (profile.ProxyJump is not null && !SshProfileRules.IsValidProxyJump(profile.ProxyJump))
        {
            issues.Add(Issue(nameof(SshProfile.ProxyJump), "The jump host list must be up to eight '[user@]host[:port]' entries separated by commas."));
        }

        if (!SshProfileRules.IsValidAbsoluteRemotePath(profile.RemoteWorkspaceRoot))
        {
            issues.Add(Issue(nameof(SshProfile.RemoteWorkspaceRoot), "The remote workspace root must be an absolute POSIX path without control characters or '..' segments."));
        }

        if (profile.RemoteOmpPath is not null && !SshProfileRules.IsValidRemoteExecutable(profile.RemoteOmpPath))
        {
            issues.Add(Issue(nameof(SshProfile.RemoteOmpPath), "The remote omp path must be an absolute POSIX path or a plain command name."));
        }

        if (profile.KeepAliveSeconds is < SshProfileRules.MinKeepAliveSeconds or > SshProfileRules.MaxKeepAliveSeconds)
        {
            issues.Add(Issue(nameof(SshProfile.KeepAliveSeconds), "The keep-alive interval must be between 5 and 600 seconds."));
        }

        ValidateOptions(profile, issues);
        return issues;
    }

    /// <summary>Returns whether a profile has no issue.</summary>
    /// <param name="profile">The profile.</param>
    public static bool IsValid(EnvironmentProfile profile) => Validate(profile).Count == 0;

    /// <summary>Returns whether an ssh profile has no issue.</summary>
    /// <param name="profile">The profile.</param>
    public static bool IsValid(SshProfile profile) => Validate(profile).Count == 0;

    private static void ValidateOptions(SshProfile profile, List<ProfileIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in profile.Options)
        {
            if (!SshProfileRules.TryGetCanonicalOptionName(name, out _))
            {
                issues.Add(Issue(nameof(SshProfile.Options), "An option name is not in the allowlist."));
                continue;
            }

            if (!seen.Add(name))
            {
                issues.Add(Issue(nameof(SshProfile.Options), "An option is set twice."));
                continue;
            }

            if (!SshProfileRules.IsAllowedOption(name, value))
            {
                issues.Add(Issue(nameof(SshProfile.Options), $"The value of the option '{name}' is not accepted."));
            }
        }
    }

    private static ProfileIssue Issue(string property, string message) => new("Ssh." + property, message);

    private static bool ContainsControl(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
