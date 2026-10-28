namespace OmpOS.Presentation.Remote;

/// <summary>The host aliases of the user's ssh configuration that can become environments.</summary>
/// <param name="Aliases">The concrete aliases (no wildcard patterns) in file order, each once.</param>
/// <param name="Warnings">Plain sentences about files that could not be read; the aliases of such a file are missing.</param>
public sealed record RemoteHostCandidates(IReadOnlyList<string> Aliases, IReadOnlyList<string> Warnings);
