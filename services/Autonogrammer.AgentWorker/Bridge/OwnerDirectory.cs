namespace Autonogrammer.AgentWorker.Bridge;

/// <summary>
/// The per-account directory a session lives under. The control plane names it in the <c>owner_dir</c> claim of the worker token: the
/// lowercase hex SHA-256 of the UTF-8 owner id (the value <c>AccountWorkspace.DirectoryName</c> computes in the API). The worker does
/// not derive it from an owner id; it accepts the claim only in exactly that shape.
/// </summary>
internal static class OwnerDirectory
{
    /// <summary>Gets the claim type that carries the owner directory name.</summary>
    public const string ClaimType = "owner_dir";

    private const int NameLength = 64;

    /// <summary>Returns whether <paramref name="value"/> is a well-formed owner directory name: exactly 64 lowercase hex characters.</summary>
    /// <param name="value">The candidate name.</param>
    /// <returns><see langword="true"/> when the value has the shape of a directory name.</returns>
    public static bool IsValid(string? value) =>
        value is { Length: NameLength } && value.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
