namespace Autonogrammer.Harness.OmpConfig;

/// <summary>What an edit of the <c>models.yml</c> text decided. <see cref="NewText"/> is set only for <see cref="OmpModelsWriteStatus.Written"/>.</summary>
/// <param name="Status">The decision.</param>
/// <param name="NewText">The complete new file text, or null when nothing is to be written.</param>
/// <param name="Message">A plain sentence for the user.</param>
internal sealed record OmpModelsEdit(OmpModelsWriteStatus Status, string? NewText, string Message);
