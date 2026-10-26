namespace OmpOS.Contracts.Ui;

/// <summary>One file edit read from a tool-call update.</summary>
/// <param name="Path">The edited file's path.</param>
/// <param name="OldText">The text before the edit, or null when the edit created the file.</param>
/// <param name="NewText">The text after the edit.</param>
public sealed record ToolFileEdit(string Path, string? OldText, string NewText);
