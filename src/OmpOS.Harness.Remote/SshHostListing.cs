namespace OmpOS.Harness.Remote;

/// <summary>The concrete host aliases of an ssh configuration and what could not be read.</summary>
public sealed record SshHostListing
{
    /// <summary>Gets the aliases in file order, each once. Wildcard patterns and negations are not aliases.</summary>
    public required IReadOnlyList<SshHostEntry> Hosts { get; init; }

    /// <summary>Gets plain-text notes about files that could not be read or directives that are not followed. The aliases of such a file are missing from <see cref="Hosts"/>.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
