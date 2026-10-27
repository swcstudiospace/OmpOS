namespace OmpOS.Api.Services;

/// <summary>The net edit of one file across a session's runs, as the harness reported it.</summary>
/// <param name="Path">The path as the harness reported it.</param>
/// <param name="OldText">The text before the first edit, or null when the edits created the file.</param>
/// <param name="NewText">The text after the latest edit.</param>
internal sealed record ReportedChange(string Path, string? OldText, string NewText);
