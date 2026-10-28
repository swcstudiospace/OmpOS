namespace OmpOS.Presentation.Mentions;

/// <summary>
/// Recognises files that usually hold secrets by name: <c>.env</c> files, private keys and credential stores. The picker never lists them; a user who types the
/// exact name is asked to confirm. The check is by name only and is a second line behind the ignore files.
/// </summary>
public static class MentionSecretFiles
{
    private static readonly string[] SecretExtensions = [".pem", ".key", ".p12", ".pfx", ".keystore", ".jks"];
    private static readonly string[] SecretNames = [".netrc", ".npmrc", ".pgpass", "credentials", "credentials.json", "secrets.json", "secrets.yml", "secrets.yaml"];
    private static readonly string[] PublicEnvSuffixes = [".example", ".sample", ".template", ".dist"];

    /// <summary>Gets whether the file name suggests a secret.</summary>
    /// <param name="relativePath">The workspace-relative path.</param>
    /// <returns><see langword="true"/> for <c>.env</c>, <c>.env.*</c> (except example files), <c>*.pem</c>, <c>*.key</c>, <c>id_*</c> (except <c>*.pub</c>) and similar.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> is null.</exception>
    public static bool IsSecretBearing(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var slash = relativePath.LastIndexOf('/');
        var name = slash >= 0 ? relativePath[(slash + 1)..] : relativePath;
        if (name.Length == 0)
        {
            return false;
        }

        if (string.Equals(name, ".env", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
        {
            return !PublicEnvSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        if (name.StartsWith("id_", StringComparison.OrdinalIgnoreCase))
        {
            return !name.EndsWith(".pub", StringComparison.OrdinalIgnoreCase);
        }

        if (SecretNames.Any(secret => string.Equals(name, secret, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return SecretExtensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }
}
