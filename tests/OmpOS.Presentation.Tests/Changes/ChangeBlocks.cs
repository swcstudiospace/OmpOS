using System.Text.Json;
using OmpOS.Contracts.Ui;

namespace OmpOS.Presentation.Tests;

/// <summary>Builds the transcript blocks the change-set tests read: file edits and the tool-call cards they belong to.</summary>
internal static class ChangeBlocks
{
    /// <summary>Builds the file-edit block of a tool call.</summary>
    /// <param name="path">The path the agent reported.</param>
    /// <param name="oldText">The text before the edit, or null for a created file.</param>
    /// <param name="newText">The text after the edit.</param>
    /// <param name="callId">The tool call that made the edit.</param>
    /// <param name="index">The edit's position within its call, which makes the block identifier.</param>
    /// <returns>The block.</returns>
    public static FileEditBlock Edit(string path, string? oldText, string newText, string callId = "call-1", int index = 0) => new()
    {
        Id = $"{callId}-edit-{index}",
        CallId = callId,
        Path = path,
        OldText = oldText,
        NewText = newText,
    };

    /// <summary>Builds the card of a tool call.</summary>
    /// <param name="callId">The call identifier.</param>
    /// <param name="status">The call status, such as <c>completed</c> or <c>failed</c>.</param>
    /// <returns>The block.</returns>
    public static ToolCallBlock Call(string callId, string status)
    {
        using var document = JsonDocument.Parse("{}");
        return new ToolCallBlock
        {
            Id = callId,
            CallId = callId,
            ToolName = "edit",
            Status = status,
            Arguments = document.RootElement.Clone(),
        };
    }
}
