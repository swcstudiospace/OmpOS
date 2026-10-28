using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>The checks the container and WSL command builders share before a script or a profile reaches a process argument list.</summary>
internal static class ExecScriptRules
{
    /// <summary>Refuses a script that is blank or spans several lines: a script is one line of POSIX shell passed to <c>sh -c</c>.</summary>
    /// <param name="script">The script.</param>
    /// <exception cref="ArgumentException">The script is blank, or contains NUL, a carriage return or a line feed.</exception>
    internal static void RequireOneLine(string script)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        if (script.Contains('\n', StringComparison.Ordinal) || script.Contains('\r', StringComparison.Ordinal) || script.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("A script passed to a container or distribution must be one line without NUL.", nameof(script));
        }
    }

    /// <summary>Throws when a container profile has an issue. The message names the properties and rules, never the rejected values.</summary>
    /// <param name="profile">The profile.</param>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    internal static void RequireValid(ContainerProfile profile)
    {
        var issues = EnvironmentProfileValidator.Validate(profile);
        if (issues.Count > 0)
        {
            throw new ArgumentException("The container profile is not valid: " + string.Join(" ", issues.Select(issue => issue.Property + ": " + issue.Message)), nameof(profile));
        }
    }

    /// <summary>Throws when a WSL profile has an issue. The message names the properties and rules, never the rejected values.</summary>
    /// <param name="profile">The profile.</param>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    internal static void RequireValid(WslProfile profile)
    {
        var issues = EnvironmentProfileValidator.Validate(profile);
        if (issues.Count > 0)
        {
            throw new ArgumentException("The WSL profile is not valid: " + string.Join(" ", issues.Select(issue => issue.Property + ": " + issue.Message)), nameof(profile));
        }
    }

    /// <summary>Refuses a launch that carries reverse port forwards: a container or distribution is not reached through ssh, so there is nothing to carry them.</summary>
    /// <param name="launch">The launch.</param>
    /// <exception cref="ArgumentException">The launch has reverse forwards.</exception>
    internal static void RequireNoForwards(RemoteLaunch launch)
    {
        if (launch.ReverseForwards.Count > 0)
        {
            throw new ArgumentException("A container or WSL launch carries no reverse port forwards; use ExecLaunchAdapter to remove them first.", nameof(launch));
        }
    }
}
