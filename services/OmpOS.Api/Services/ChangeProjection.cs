using System.Text.Json;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;

namespace OmpOS.Api.Services;

/// <summary>
/// Reads the file edits a session's runs reported out of its log, the way a run's output reads them, and merges them per file: the text
/// before the first edit and the text after the latest one. A file whose edits cancel out has no change.
/// </summary>
internal static class ChangeProjection
{
    private const string ToolCallUpdateKind = "tool_call_update";
    private const string FailedToolStatus = "failed";

    /// <summary>Collects the net changes of a session.</summary>
    /// <param name="session">The session, as currently stored.</param>
    /// <returns>One change per file, in the order of the file's first edit.</returns>
    public static IReadOnlyList<ReportedChange> Collect(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var edits = new List<ReportedChange>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var evt in session.Events)
        {
            switch (evt.Payload)
            {
                case UiBlockPayload { Block: FileEditBlock block }:
                    Put(edits, positions, block.Id, new ReportedChange(block.Path, block.OldText, block.NewText));
                    break;
                case UnknownPayload { OriginalKind: ToolCallUpdateKind } update:
                    PutUpdate(edits, positions, update.Raw);
                    break;
            }
        }

        var merged = new List<ReportedChange>();
        var byPath = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var edit in edits)
        {
            if (byPath.TryGetValue(edit.Path, out var index))
            {
                merged[index] = merged[index] with { NewText = edit.NewText };
            }
            else
            {
                byPath[edit.Path] = merged.Count;
                merged.Add(edit);
            }
        }

        return [.. merged.Where(static change => !string.Equals(change.OldText, change.NewText, StringComparison.Ordinal))];
    }

    private static void PutUpdate(List<ReportedChange> edits, Dictionary<string, int> positions, JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object
            || StringOf(raw, "callId") is not { } callId
            || StringOf(raw, "payloadJson") is not { } payloadJson
            || string.Equals(StringOf(raw, "status"), FailedToolStatus, StringComparison.Ordinal))
        {
            return;
        }

        var found = ToolUpdateContent.Read(payloadJson).Edits;
        for (var i = 0; i < found.Count; i++)
        {
            Put(edits, positions, $"{callId}-edit-{i}", new ReportedChange(found[i].Path, found[i].OldText, found[i].NewText));
        }
    }

    private static void Put(List<ReportedChange> edits, Dictionary<string, int> positions, string key, ReportedChange change)
    {
        if (positions.TryGetValue(key, out var index))
        {
            edits[index] = change;
            return;
        }

        positions[key] = edits.Count;
        edits.Add(change);
    }

    private static string? StringOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
