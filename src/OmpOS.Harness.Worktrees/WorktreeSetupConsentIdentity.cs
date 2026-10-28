using System.Security.Cryptography;
using System.Text;

namespace OmpOS.Worktrees;

/// <summary>
/// What a consent to run a worktree's setup applies to: one repository and the exact setup that would run, which is the bytes of <c>.cursor/worktrees.json</c> and, when the entry is a script,
/// the bytes of the script. A different repository or a changed file or script is a different identity, so it needs its own consent. The host keeps the answer in the setup consent store it
/// already has, using <see cref="StoreRepositoryPath"/> and <see cref="Sha256"/> as the key, so a worktree setup approval never replaces the approval of the repository's <c>.ompos/setup.json</c>.
/// </summary>
public sealed record WorktreeSetupConsentIdentity
{
    private WorktreeSetupConsentIdentity(string repositoryPath, string sha256)
    {
        RepositoryPath = repositoryPath;
        Sha256 = sha256;
    }

    /// <summary>Gets the absolute path of the repository's main checkout.</summary>
    public string RepositoryPath { get; }

    /// <summary>Gets the lowercase hexadecimal SHA-256 over the configuration file and, for a script entry, the script.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the path to give the setup consent store as the repository: the configuration file itself, so it differs from the repository path that <c>.ompos/setup.json</c> approvals use.</summary>
    public string StoreRepositoryPath => Path.Combine(RepositoryPath, ".cursor", "worktrees.json");

    /// <summary>Works out the identity of the setup a configuration would run.</summary>
    /// <param name="projectRoot">The main checkout the worktree was made from.</param>
    /// <param name="config">The configuration that was found.</param>
    /// <param name="setup">The entry that would run on this operating system.</param>
    /// <returns>The identity.</returns>
    /// <exception cref="IOException">The configuration or script cannot be read.</exception>
    public static WorktreeSetupConsentIdentity Create(string projectRoot, CursorWorktreesConfig config, CursorWorktreeSetup setup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(setup);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, File.ReadAllBytes(config.FilePath));
        if (setup.ScriptPath is { } script)
        {
            Append(hash, File.ReadAllBytes(script));
        }

        return new WorktreeSetupConsentIdentity(Path.GetFullPath(projectRoot), Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    private static void Append(IncrementalHash hash, byte[] content)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(content.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"));
        hash.AppendData(content);
    }
}
