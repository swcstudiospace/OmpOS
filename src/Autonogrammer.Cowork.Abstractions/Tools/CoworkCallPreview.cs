namespace Autonogrammer.Cowork.Abstractions;

/// <summary>What the host observed about a call before it runs. Target is host-observed, never model text.</summary>
/// <param name="Headline">The one-line summary shown on the approval card.</param>
/// <param name="Target">The host-observed target, such as a URL or window title, or null.</param>
/// <param name="Details">Further host-observed lines.</param>
/// <param name="Grant">The grant an allow-always answer would store, or null.</param>
public sealed record CoworkCallPreview(string Headline, string? Target, IReadOnlyList<string> Details, CoworkGrantKey? Grant);
