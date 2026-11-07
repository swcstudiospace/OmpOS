namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>One origin as the Access dialog shows it: the ASCII (punycode) form, the decoded form and the script mark.</summary>
/// <param name="Ascii">The ASCII form. It is what the network sees.</param>
/// <param name="Decoded">The decoded Unicode form, or the ASCII form when it does not decode.</param>
/// <param name="MixedScript">Whether one label mixes two scripts, a common spoofing sign.</param>
public sealed record ArtifactOriginView(string Ascii, string Decoded, bool MixedScript);
