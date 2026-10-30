namespace Autonogrammer.Harness.OmpConfig;

/// <summary>The managed providers found in <c>models.yml</c>.</summary>
/// <param name="Providers">The providers read from Autonogrammer blocks, in file order.</param>
/// <param name="Problems">One plain sentence per block that could not be read back, or for a layout this editor does not support. Empty when everything read.</param>
public sealed record OmpManagedReadResult(IReadOnlyList<OmpManagedProvider> Providers, IReadOnlyList<string> Problems);
