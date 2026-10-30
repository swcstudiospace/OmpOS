using System.Text.Json;

namespace Autonogrammer.Workspace;

/// <summary>
/// Loads rules, skills and modes from the convention folders. A malformed file produces one diagnostic and is
/// skipped; it never fails the load.
/// </summary>
internal static class WorkspaceLoader
{
    /// <summary>
    /// Loads every convention file under the root.
    /// </summary>
    public static WorkspaceSnapshot Load(WorkspaceRoot root)
    {
        var diagnostics = new List<WorkspaceDiagnostic>();
        var rules = LoadDocuments(root, WorkspaceConventions.RulesFolder, diagnostics);
        var skills = LoadDocuments(root, WorkspaceConventions.SkillsFolder, diagnostics);
        var modes = LoadModes(root, rules, skills, diagnostics);

        var orderedDiagnostics = diagnostics
            .OrderBy(diagnostic => diagnostic.RelativePath, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Reason, StringComparer.Ordinal)
            .ToList();
        return new WorkspaceSnapshot(rules, skills, modes, orderedDiagnostics);
    }

    private static List<WorkspaceDocument> LoadDocuments(
        WorkspaceRoot root,
        string folder,
        List<WorkspaceDiagnostic> diagnostics)
    {
        var documents = new List<WorkspaceDocument>();
        foreach (var relativePath in WorkspaceFiles.EnumerateConventionFiles(root, folder, ".md", diagnostics))
        {
            var id = Path.GetFileNameWithoutExtension(relativePath);
            if (!WorkspaceConventions.IsValidId(id))
            {
                diagnostics.Add(new WorkspaceDiagnostic(
                    relativePath,
                    $"the file name '{id}' is not a valid id; use 1 to 64 lowercase letters, digits, '-' or '_', starting with a letter or digit"));
                continue;
            }

            var (text, reason) = WorkspaceFiles.ReadText(root, relativePath);
            if (text is null)
            {
                diagnostics.Add(new WorkspaceDiagnostic(relativePath, reason!));
                continue;
            }

            documents.Add(new WorkspaceDocument(id, relativePath, text));
        }

        return documents;
    }

    private static List<WorkspaceMode> LoadModes(
        WorkspaceRoot root,
        IReadOnlyList<WorkspaceDocument> rules,
        IReadOnlyList<WorkspaceDocument> skills,
        List<WorkspaceDiagnostic> diagnostics)
    {
        var ruleIds = rules.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        var skillIds = skills.Select(skill => skill.Id).ToHashSet(StringComparer.Ordinal);
        var definedBy = new Dictionary<string, string>(StringComparer.Ordinal);
        var modes = new List<WorkspaceMode>();

        foreach (var relativePath in WorkspaceFiles.EnumerateConventionFiles(root, WorkspaceConventions.ModesFolder, ".json", diagnostics))
        {
            var (text, readReason) = WorkspaceFiles.ReadText(root, relativePath);
            if (text is null)
            {
                diagnostics.Add(new WorkspaceDiagnostic(relativePath, readReason!));
                continue;
            }

            var (mode, reason) = ParseMode(relativePath, text, ruleIds, skillIds);
            if (mode is null)
            {
                diagnostics.Add(new WorkspaceDiagnostic(relativePath, reason!));
                continue;
            }

            if (definedBy.TryGetValue(mode.Id, out var firstPath))
            {
                diagnostics.Add(new WorkspaceDiagnostic(
                    relativePath,
                    $"the mode id '{mode.Id}' is already defined by '{firstPath}'"));
                continue;
            }

            definedBy.Add(mode.Id, relativePath);
            modes.Add(mode);
        }

        return modes;
    }

    private static (WorkspaceMode? Mode, string? Reason) ParseMode(
        string relativePath,
        string text,
        IReadOnlySet<string> ruleIds,
        IReadOnlySet<string> skillIds)
    {
        ModeFile? file;
        try
        {
            file = JsonSerializer.Deserialize(text, WorkspaceJsonContext.Default.ModeFile);
        }
        catch (JsonException exception)
        {
            return (null, $"invalid JSON: {exception.Message}");
        }

        if (file is null)
        {
            return (null, "the mode must be a JSON object");
        }

        if (file.Id is null)
        {
            return (null, "the mode is missing 'id'");
        }

        if (!WorkspaceConventions.IsValidId(file.Id))
        {
            return (null, $"the mode id '{file.Id}' is not a valid id; use 1 to 64 lowercase letters, digits, '-' or '_', starting with a letter or digit");
        }

        var displayName = file.DisplayName?.Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            return (null, "the mode is missing 'displayName'");
        }

        if (displayName.Length > WorkspaceConventions.MaxDisplayNameLength)
        {
            return (null, $"the mode 'displayName' is longer than {WorkspaceConventions.MaxDisplayNameLength} characters");
        }

        var (rules, ruleReason) = ReferencedIds(file.Rules, "rules", "rule", ruleIds);
        if (rules is null)
        {
            return (null, ruleReason);
        }

        var (skills, skillReason) = ReferencedIds(file.Skills, "skills", "skill", skillIds);
        if (skills is null)
        {
            return (null, skillReason);
        }

        return (new WorkspaceMode(file.Id, displayName, relativePath, rules, skills), null);
    }

    private static (IReadOnlyList<string>? Ids, string? Reason) ReferencedIds(
        List<string?>? values,
        string field,
        string kind,
        IReadOnlySet<string> known)
    {
        var ids = new List<string>();
        foreach (var value in values ?? [])
        {
            if (value is null)
            {
                return (null, $"the mode '{field}' list contains a null entry");
            }

            if (!known.Contains(value))
            {
                return (null, $"the mode references unknown {kind} '{value}'");
            }

            if (!ids.Contains(value, StringComparer.Ordinal))
            {
                ids.Add(value);
            }
        }

        return (ids, null);
    }
}
