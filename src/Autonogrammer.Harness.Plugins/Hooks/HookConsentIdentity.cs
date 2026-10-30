using System.Security.Cryptography;
using System.Text.Json;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Computes the consent identity of a <c>hooks.json</c> hook. Version 2 never contains an absolute path, so the same
/// repository gives the same hash on a laptop, in a sandbox and under another checkout directory. Version 1
/// (<see cref="HookCommandIdentity"/>) stays in force for plugin manifest hooks, and version 1 consent records stay
/// readable: both versions live in the same persisted set of hashes and cannot collide.
/// </summary>
public static class HookConsentIdentity
{
    /// <summary>The version this class writes.</summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// Returns the lowercase SHA-256 hex digest of the hook's canonical JSON form. Changing the event, scope, command,
    /// arguments, shell, relative working directory, environment, type, prompt, failure policy, matcher, timeout, loop
    /// limit, locations or the content of a script it names gives a new digest, so consent for the old one does not apply.
    /// The enabled flag, description and id are not hashed.
    /// </summary>
    /// <param name="identity">The hook's identity input.</param>
    /// <returns>A 64-character lowercase hex string.</returns>
    public static string Compute(HookIdentityInput identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var document = new HookIdentityV2Document
        {
            HashVersion = CurrentVersion,
            Event = identity.EventName,
            Omp = identity.IsOmpEvent,
            Scope = identity.Scope.ToString(),
            Type = identity.Type.ToString(),
            Command = identity.Command,
            Args = identity.Args is null ? null : [.. identity.Args],
            Shell = identity.Shell.ToString(),
            Cwd = identity.RelativeWorkingDirectory,
            Env = [.. identity.Env.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}")],
            Prompt = identity.Prompt,
            Model = identity.Model,
            FailClosed = identity.FailClosed,
            Matcher = identity.Matcher,
            TimeoutSeconds = identity.TimeoutSeconds,
            LoopLimit = identity.LoopLimit,
            RunsIn = (int)identity.RunsIn,
            Executables = [.. identity.Executables
                .OrderBy(digest => digest.RelativePath, StringComparer.Ordinal)
                .Select(digest => $"{digest.RelativePath}={digest.Sha256}")],
        };
        var canonical = JsonSerializer.SerializeToUtf8Bytes(document, HooksJsonContext.Default.HookIdentityV2Document);
        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    /// <summary>
    /// Returns a directory relative to its scope root with <c>/</c> separators, <c>.</c> for the root itself.
    /// </summary>
    /// <param name="scopeRoot">The absolute scope root.</param>
    /// <param name="workingDirectory">The absolute working directory.</param>
    /// <returns>The relative form used in the identity.</returns>
    public static string RelativeDirectory(string scopeRoot, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(scopeRoot);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        var relative = Path.GetRelativePath(scopeRoot, workingDirectory).Replace('\\', '/');
        return relative.Length == 0 ? "." : relative;
    }
}
