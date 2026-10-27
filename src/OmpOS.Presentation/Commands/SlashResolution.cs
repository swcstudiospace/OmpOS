namespace OmpOS.Presentation.Commands;

/// <summary>What typed composer text means. The resolver returns one of <see cref="SlashRunAction"/>, <see cref="SlashSendPrompt"/>, <see cref="SlashRefused"/> or <see cref="SlashPassThrough"/>.</summary>
public abstract record SlashResolution;
