namespace Autonogrammer.Native.Settings.Models;

/// <summary>A local runtime the engine discovers by itself on a loopback address. The engine lists its models only while the runtime answers.</summary>
/// <param name="Id">The provider id the engine uses.</param>
/// <param name="DisplayName">The name shown on the card.</param>
/// <param name="Endpoint">The address the engine uses when no environment variable moves it (the engine's documented default).</param>
internal sealed record LocalRuntime(string Id, string DisplayName, Uri Endpoint);
