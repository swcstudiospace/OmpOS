namespace OmpOS.Presentation.Safety;

/// <summary>One file the page looked at.</summary>
/// <param name="Path">The file's path.</param>
/// <param name="Scope">The word for its scope: User or Project.</param>
/// <param name="Kind">The file's name: permissions.json or sandbox.json.</param>
/// <param name="Status">What happened to it: read, not found, or ignored and why.</param>
/// <param name="IsWinning">True when this file sets the read boundary or the read allowlist in force.</param>
public sealed record RunModeFileRow(string Path, string Scope, string Kind, string Status, bool IsWinning)
{
    /// <summary>Gets the line as one sentence for an accessible name.</summary>
    public string Summary => Scope + " " + Kind + ", " + Path + ": " + Status + (IsWinning ? " It sets a value in force." : string.Empty);
}
