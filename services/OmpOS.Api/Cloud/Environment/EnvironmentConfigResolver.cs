namespace OmpOS.Api.Cloud;

/// <summary>
/// Chooses the environment configuration of an agent. The order follows Cursor's: a personal definition first, then a team definition (not available
/// in OmpOS: there are no teams), then the repository file. A repository file applies only to a single-repository agent, as in Cursor; between the two
/// repository files <c>.ompos/environment.json</c> wins over <c>.cursor/environment.json</c>. A chosen file that is invalid is reported, never skipped.
/// </summary>
internal static class EnvironmentConfigResolver
{
    /// <summary>Resolves the configuration.</summary>
    /// <param name="personal">The personal (saved, named) definition the agent selected, or null.</param>
    /// <param name="repositoryCount">How many repositories the agent works in.</param>
    /// <param name="ompOsFile">The text of <c>.ompos/environment.json</c> in the primary repository, or null when absent.</param>
    /// <param name="cursorFile">The text of <c>.cursor/environment.json</c> in the primary repository, or null when absent.</param>
    /// <returns>The chosen configuration, or the errors of the chosen file.</returns>
    public static ResolvedEnvironment Resolve(EnvironmentConfig? personal, int repositoryCount, string? ompOsFile, string? cursorFile)
    {
        if (personal is not null)
        {
            return new ResolvedEnvironment(personal, [], BuildWarnings(personal));
        }

        if (repositoryCount != 1)
        {
            return new ResolvedEnvironment(null, [], []);
        }

        foreach (var (path, text) in new[] { (EnvironmentConfigFile.OmpOsPath, ompOsFile), (EnvironmentConfigFile.CursorPath, cursorFile) })
        {
            if (text is null)
            {
                continue;
            }

            var parsed = EnvironmentConfigFile.Parse(text, path);
            if (parsed.Config is null)
            {
                return new ResolvedEnvironment(null, parsed.Errors.Select(error => $"{path}: {error}").ToList(), parsed.Warnings);
            }

            return new ResolvedEnvironment(parsed.Config, [], [.. parsed.Warnings, .. BuildWarnings(parsed.Config)]);
        }

        return new ResolvedEnvironment(null, [], []);
    }

    private static List<string> BuildWarnings(EnvironmentConfig config)
    {
        var warnings = new List<string>();
        if (config.Build is not null)
        {
            warnings.Add("build.dockerfile is not supported on a Railway worker (no Docker daemon); the build is ignored.");
        }

        if (config.Snapshot is not null)
        {
            warnings.Add("snapshot is not supported on a Railway worker; it is ignored.");
        }

        return warnings;
    }
}
