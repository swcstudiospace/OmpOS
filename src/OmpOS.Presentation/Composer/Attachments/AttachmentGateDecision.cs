namespace OmpOS.Presentation.Composer.Attachments;

/// <summary>What the gate lets the composer do with images.</summary>
/// <param name="CanAttach">Whether images may be attached.</param>
/// <param name="Reason">Why attaching is refused; empty when it is allowed.</param>
/// <param name="Note">A caution shown while attaching is allowed; empty when there is none.</param>
public sealed record AttachmentGateDecision(bool CanAttach, string Reason, string Note);
