namespace OmpOS.Presentation.Commands;

/// <summary>The text names a command that OmpOS knows and cannot run. Nothing is sent: the reason is shown instead.</summary>
/// <param name="Name">The name typed after the slash, or <c>&amp;</c> for the cloud hand-off prefix.</param>
/// <param name="Reason">The reason, ready to show.</param>
public sealed record SlashRefused(string Name, string Reason) : SlashResolution;
