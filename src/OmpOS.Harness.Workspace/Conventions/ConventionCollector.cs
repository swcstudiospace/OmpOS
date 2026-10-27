namespace OmpOS.Workspace.Conventions;

/// <summary>
/// Collects the items of one discovery run. It is fed in precedence order, so the first item with a name wins and a later one with the same name is recorded
/// as a <see cref="ConventionDiagnosticKind.Duplicate"/> diagnostic. It is not thread safe: one run uses one collector.
/// </summary>
internal sealed class ConventionCollector
{
    private readonly Dictionary<(ConventionItemKind Kind, string Key), string> _winners = [];
    private readonly List<ConventionCommand> _commands = [];
    private readonly List<ConventionSkill> _skills = [];
    private readonly List<ConventionRule> _rules = [];
    private readonly List<ConventionSubagent> _subagents = [];
    private readonly List<ConventionConfigFile> _hookFiles = [];
    private readonly List<ConventionConfigFile> _mcpFiles = [];
    private readonly List<ConventionDiagnostic> _diagnostics = [];

    /// <summary>Records a problem.</summary>
    /// <param name="item">The kind of file it belongs to.</param>
    /// <param name="origin">The path to show.</param>
    /// <param name="kind">What went wrong.</param>
    /// <param name="reason">One plain sentence that says why.</param>
    public void AddDiagnostic(ConventionItemKind item, string origin, ConventionDiagnosticKind kind, string reason) =>
        _diagnostics.Add(new ConventionDiagnostic(item, origin, kind, reason));

    /// <summary>Adds a command unless its name is taken.</summary>
    /// <param name="command">The command.</param>
    public void Add(ConventionCommand command)
    {
        if (Claim(ConventionItemKind.Command, command.Name, command.Name, command.Origin))
        {
            _commands.Add(command);
        }
    }

    /// <summary>Adds a skill unless its name is taken in the same scope folder.</summary>
    /// <param name="skill">The skill.</param>
    public void Add(ConventionSkill skill)
    {
        if (Claim(ConventionItemKind.Skill, skill.ScopeDirectory + "|" + skill.Name, skill.Name, skill.Origin))
        {
            _skills.Add(skill);
        }
    }

    /// <summary>Adds a rule unless its name is taken in the same scope folder.</summary>
    /// <param name="rule">The rule.</param>
    public void Add(ConventionRule rule)
    {
        if (Claim(ConventionItemKind.Rule, rule.ScopeDirectory + "|" + rule.Name, rule.Name, rule.Origin))
        {
            _rules.Add(rule);
        }
    }

    /// <summary>Adds a subagent unless its name is taken.</summary>
    /// <param name="subagent">The subagent.</param>
    public void Add(ConventionSubagent subagent)
    {
        if (Claim(ConventionItemKind.Subagent, subagent.Name, subagent.Name, subagent.Origin))
        {
            _subagents.Add(subagent);
        }
    }

    /// <summary>Lists a hooks file.</summary>
    /// <param name="file">The file.</param>
    public void AddHookFile(ConventionConfigFile file) => _hookFiles.Add(file);

    /// <summary>Lists an MCP file.</summary>
    /// <param name="file">The file.</param>
    public void AddMcpFile(ConventionConfigFile file) => _mcpFiles.Add(file);

    /// <summary>Builds the result. Items keep project-then-user order and are sorted by name within a scope.</summary>
    /// <returns>The set.</returns>
    public ConventionSet Build() => new(
        [.. _commands.OrderBy(static item => item.Scope).ThenBy(static item => item.Name, StringComparer.Ordinal)],
        [.. _skills.OrderBy(static item => item.Scope).ThenBy(static item => item.Name, StringComparer.Ordinal).ThenBy(static item => item.Origin, StringComparer.Ordinal)],
        [.. _rules.OrderBy(static item => item.Scope).ThenBy(static item => item.Name, StringComparer.Ordinal).ThenBy(static item => item.Origin, StringComparer.Ordinal)],
        [.. _subagents.OrderBy(static item => item.Scope).ThenBy(static item => item.Name, StringComparer.Ordinal)],
        [.. _hookFiles],
        [.. _mcpFiles],
        [.. _diagnostics]);

    private bool Claim(ConventionItemKind kind, string key, string name, string origin)
    {
        if (_winners.TryGetValue((kind, key), out var winner))
        {
            AddDiagnostic(kind, origin, ConventionDiagnosticKind.Duplicate, "'" + name + "' is already provided by " + winner);
            return false;
        }

        _winners[(kind, key)] = origin;
        return true;
    }
}
