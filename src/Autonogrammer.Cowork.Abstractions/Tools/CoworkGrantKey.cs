namespace Autonogrammer.Cowork.Abstractions;

/// <summary>A stored grant key, for example browser origin and a URL.</summary>
/// <param name="Scope">The grant family.</param>
/// <param name="Value">The granted value.</param>
public sealed record CoworkGrantKey(string Scope, string Value);
