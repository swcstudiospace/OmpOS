namespace Autonogrammer.Presentation.Composer.Attachments;

/// <summary>Decides whether images may be attached, from the engine's prompt capability and the selected model's vision flag.</summary>
public static class AttachmentGate
{
    /// <summary>Evaluates the gate.</summary>
    /// <param name="promptImageCapability">The engine's ACP <c>promptCapabilities.image</c>, or <see langword="null"/> when not reported yet.</param>
    /// <param name="vision">Whether the selected model reads images.</param>
    /// <returns>The decision. An engine that says no images wins over any model flag; an unknown model is allowed with a note.</returns>
    public static AttachmentGateDecision Evaluate(bool? promptImageCapability, VisionSupport vision)
    {
        if (promptImageCapability == false)
        {
            return new AttachmentGateDecision(false, AttachmentCopy.EngineRefuses, string.Empty);
        }

        return vision switch
        {
            VisionSupport.No => new AttachmentGateDecision(false, AttachmentCopy.ModelRefuses, string.Empty),
            VisionSupport.Unknown => new AttachmentGateDecision(true, string.Empty, AttachmentCopy.ModelMayIgnore),
            _ => new AttachmentGateDecision(true, string.Empty, string.Empty),
        };
    }
}
