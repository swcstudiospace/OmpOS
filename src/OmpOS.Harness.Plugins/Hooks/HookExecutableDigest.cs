namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// The content digest of a file a hook command names inside its scope root. It goes into the consent identity, so editing
/// the script voids consent: neither a person nor the agent can swap a consented script unseen.
/// </summary>
/// <param name="RelativePath">The path relative to the scope root, with <c>/</c> separators.</param>
/// <param name="Sha256">The lowercase SHA-256 hex digest of the file bytes.</param>
public sealed record HookExecutableDigest(string RelativePath, string Sha256);
