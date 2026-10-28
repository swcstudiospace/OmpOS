namespace OmpOS.Presentation.Composer.Attachments;

/// <summary>Whether the selected model reads images.</summary>
public enum VisionSupport
{
    /// <summary>The model info does not say.</summary>
    Unknown,

    /// <summary>The model lists image input.</summary>
    Yes,

    /// <summary>The model lists text input only.</summary>
    No,
}
