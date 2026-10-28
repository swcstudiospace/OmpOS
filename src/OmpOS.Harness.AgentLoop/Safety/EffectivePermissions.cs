namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>
/// The permission files merged: lists are concatenated (Cursor's rule), the user's and the project's rules both apply, and a deny
/// rule from any file stands: no file removes another's deny. When the project is not trusted, its files can add deny rules and block
/// instructions only; their allow rules are ignored and reported.
/// </summary>
public sealed class EffectivePermissions
{
    private EffectivePermissions(
        IReadOnlyList<PermissionRule> allowRules,
        IReadOnlyList<PermissionRule> denyRules,
        IReadOnlyList<SafetyEntry> terminalAllowlist,
        IReadOnlyList<SafetyEntry> mcpAllowlist,
        IReadOnlyList<SafetyEntry> allowInstructions,
        IReadOnlyList<SafetyEntry> blockInstructions,
        IReadOnlyList<PermissionsFileDocument> files,
        IReadOnlyList<SafetyDiagnostic> diagnostics)
    {
        AllowRules = allowRules;
        DenyRules = denyRules;
        TerminalAllowlist = terminalAllowlist;
        McpAllowlist = mcpAllowlist;
        AllowInstructions = allowInstructions;
        BlockInstructions = blockInstructions;
        Files = files;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets permissions with no file: no rule, no entry.</summary>
    public static EffectivePermissions Empty { get; } = new([], [], [], [], [], [], [], []);

    /// <summary>Gets every allow rule: the allowlists as tokens first, then the <c>permissions.allow</c> tokens.</summary>
    public IReadOnlyList<PermissionRule> AllowRules { get; }

    /// <summary>Gets every deny rule.</summary>
    public IReadOnlyList<PermissionRule> DenyRules { get; }

    /// <summary>Gets the terminal allowlist entries, with their files.</summary>
    public IReadOnlyList<SafetyEntry> TerminalAllowlist { get; }

    /// <summary>Gets the MCP allowlist entries, with their files.</summary>
    public IReadOnlyList<SafetyEntry> McpAllowlist { get; }

    /// <summary>Gets the sentences that say what Auto-review should lean towards allowing. OmpOS has no classifier, so they are kept and shown, never enforced.</summary>
    public IReadOnlyList<SafetyEntry> AllowInstructions { get; }

    /// <summary>Gets the sentences that say what Auto-review should lean towards blocking. OmpOS has no classifier, so they are kept and shown, never enforced.</summary>
    public IReadOnlyList<SafetyEntry> BlockInstructions { get; }

    /// <summary>Gets every file that was looked at, lowest precedence first, existing or not.</summary>
    public IReadOnlyList<PermissionsFileDocument> Files { get; }

    /// <summary>Gets everything that was ignored while reading and merging.</summary>
    public IReadOnlyList<SafetyDiagnostic> Diagnostics { get; }

    private static bool HasAllows(PermissionsFileDocument document) =>
        document.TerminalAllowlist is { Count: > 0 } || document.McpAllowlist is { Count: > 0 } || document.AllowTokens.Count > 0 || document.AllowInstructions.Count > 0;

    /// <summary>Merges documents given lowest precedence first.</summary>
    /// <param name="documents">The documents.</param>
    /// <param name="trustProject">False to ignore the allow rules of project files.</param>
    /// <returns>The merged permissions.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> is null.</exception>
    public static EffectivePermissions Merge(IReadOnlyList<PermissionsFileDocument> documents, bool trustProject)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var allow = new List<PermissionRule>();
        var deny = new List<PermissionRule>();
        var terminal = new List<SafetyEntry>();
        var mcp = new List<SafetyEntry>();
        var allowInstructions = new List<SafetyEntry>();
        var blockInstructions = new List<SafetyEntry>();
        var diagnostics = new List<SafetyDiagnostic>();
        foreach (var document in documents)
        {
            diagnostics.AddRange(document.Diagnostics);
            var path = document.Location.Path;
            var scope = document.Location.Scope;
            var origin = document.Location.Origin;
            foreach (var token in document.DenyTokens)
            {
                AddRule(deny, new PermissionRule(token, path, scope));
            }

            foreach (var sentence in document.BlockInstructions)
            {
                AddEntry(blockInstructions, new SafetyEntry(sentence, path, scope, origin));
            }

            var ignoreAllows = scope == PermissionScope.Project && !trustProject;
            if (ignoreAllows)
            {
                if (HasAllows(document))
                {
                    diagnostics.Add(new SafetyDiagnostic(path, "The project is not trusted, so its allow entries are ignored. Its deny entries still apply.", SafetyDiagnosticSeverity.Warning));
                }

                continue;
            }

            foreach (var entry in document.TerminalAllowlist ?? [])
            {
                AddEntry(terminal, new SafetyEntry(entry, path, scope, origin));
                AddRule(allow, new PermissionRule(new PermissionToken(PermissionTokenKind.Shell, entry), path, scope));
            }

            foreach (var entry in document.McpAllowlist ?? [])
            {
                AddEntry(mcp, new SafetyEntry(entry, path, scope, origin));
                AddRule(allow, new PermissionRule(new PermissionToken(PermissionTokenKind.Mcp, entry), path, scope));
            }

            foreach (var token in document.AllowTokens)
            {
                AddRule(allow, new PermissionRule(token, path, scope));
            }

            foreach (var sentence in document.AllowInstructions)
            {
                AddEntry(allowInstructions, new SafetyEntry(sentence, path, scope, origin));
            }
        }

        return new EffectivePermissions(allow, deny, terminal, mcp, allowInstructions, blockInstructions, documents, diagnostics);
    }

    private static void AddRule(List<PermissionRule> rules, PermissionRule rule)
    {
        if (!rules.Exists(existing => existing.Token == rule.Token))
        {
            rules.Add(rule);
        }
    }

    private static void AddEntry(List<SafetyEntry> entries, SafetyEntry entry)
    {
        if (!entries.Exists(existing => string.Equals(existing.Text, entry.Text, StringComparison.Ordinal)))
        {
            entries.Add(entry);
        }
    }
}
