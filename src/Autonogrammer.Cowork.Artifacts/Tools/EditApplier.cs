namespace Autonogrammer.Cowork.Artifacts.Tools;

/// <summary>Applies <c>{old,new}</c> edits. Each <c>old</c> must match the current text exactly once, or nothing is applied.</summary>
public static class EditApplier
{
    /// <summary>One exact-match replacement.</summary>
    /// <param name="Old">The text that must occur once.</param>
    /// <param name="New">The replacement.</param>
    internal readonly record struct Edit(string Old, string New);

    /// <summary>Applies every edit, or applies none.</summary>
    /// <param name="content">The current text.</param>
    /// <param name="edits">The edits, in order.</param>
    /// <param name="updated">The result when every edit matched once.</param>
    /// <param name="error">A typed code when an edit matched zero times or more than once.</param>
    /// <returns>True when every edit was applied.</returns>
    internal static bool TryApply(string content, IReadOnlyList<Edit> edits, out string updated, out string? error)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(edits);
        updated = content;
        error = null;
        if (edits.Count == 0)
        {
            error = "edit_empty";
            return false;
        }

        var working = content;
        foreach (var edit in edits)
        {
            if (edit.Old.Length == 0)
            {
                error = "edit_empty";
                return false;
            }

            var count = Count(working, edit.Old);
            if (count != 1)
            {
                error = count == 0 ? "edit_not_found" : "edit_ambiguous";
                updated = content;
                return false;
            }

            var index = working.IndexOf(edit.Old, StringComparison.Ordinal);
            working = string.Concat(working.AsSpan(0, index), edit.New, working.AsSpan(index + edit.Old.Length));
        }

        updated = working;
        return true;
    }

    private static int Count(string content, string needle)
    {
        var count = 0;
        var start = 0;
        while (start <= content.Length - needle.Length)
        {
            var index = content.IndexOf(needle, start, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            start = index + needle.Length;
        }

        return count;
    }
}
