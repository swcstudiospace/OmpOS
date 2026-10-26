namespace OmpOS.Harness.Secrets;

/// <summary>
/// The contents of the file <see cref="OwnerOnlyFileSecretStore"/> keeps: a format version and the secret values as plain strings.
/// This is the only place a secret is written as plain text; <see cref="SecretString"/> itself never serialises, and this type
/// is private to the store.
/// </summary>
internal sealed class SecretFileDocument
{
    /// <summary>The only format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the format version.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the secret values by name.</summary>
    public Dictionary<string, string>? Secrets { get; set; }
}
