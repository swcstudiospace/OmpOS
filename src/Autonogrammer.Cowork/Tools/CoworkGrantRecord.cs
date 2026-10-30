namespace Autonogrammer.Cowork.Tools;

/// <summary>One stored grant.</summary>
/// <param name="Scope">The grant family.</param>
/// <param name="Value">The granted value.</param>
public sealed record CoworkGrantRecord(string Scope, string Value);
