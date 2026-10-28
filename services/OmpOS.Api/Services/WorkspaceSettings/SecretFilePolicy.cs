namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>
/// The names the workspace tree and file endpoints never show, whatever the folder holds: environment files, private keys and certificates, credential
/// files, and the folders that keep them. A path with one such segment is answered as if it did not exist.
/// </summary>
internal static class SecretFilePolicy
{
    private static readonly string[] ExactNames = [".git", ".ssh", ".aws", ".gnupg", ".netrc", ".npmrc", ".pypirc", ".git-credentials"];

    private static readonly char[] Separators = ['/', '\\'];

    private static readonly string[] Prefixes = [".env", "id_", "credentials"];

    private static readonly string[] Suffixes = [".pem", ".key", ".p12", ".pfx"];

    /// <summary>Returns whether one file or folder name is excluded.</summary>
    /// <param name="name">The name without its folder.</param>
    /// <returns>True when the name is a secret name.</returns>
    public static bool IsSecretName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ExactNames.Any(exact => string.Equals(name, exact, StringComparison.OrdinalIgnoreCase))
            || Prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || Suffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns whether any segment of a workspace-relative path is excluded.</summary>
    /// <param name="relativePath">The path, with <c>/</c> or the platform separator.</param>
    /// <returns>True when a segment is a secret name.</returns>
    public static bool IsSecretPath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return relativePath.Split(Separators, StringSplitOptions.RemoveEmptyEntries).Any(IsSecretName);
    }
}
