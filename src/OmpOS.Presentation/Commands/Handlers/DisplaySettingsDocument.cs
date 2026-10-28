namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>The persisted display switches. <see cref="Version"/> changes when the shape does; a file with another version reads as the defaults.</summary>
internal sealed class DisplaySettingsDocument
{
    public int Version { get; set; }

    public bool VimMode { get; set; }

    public bool ShowLineNumbers { get; set; } = true;

    public bool ShowThinkingBlocks { get; set; } = true;

    public bool ShowStatusIndicators { get; set; } = true;

    public bool ShowStatusLineRunningTime { get; set; } = true;

    public bool RewindEnabled { get; set; } = true;
}
