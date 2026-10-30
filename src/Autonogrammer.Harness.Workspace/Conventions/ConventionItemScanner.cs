using System.Collections.Frozen;

namespace Autonogrammer.Workspace.Conventions;

/// <summary>Reads the files of one convention folder (<c>skills</c>, <c>rules</c>, <c>commands</c>, <c>agents</c>) and the single-file conventions into items.</summary>
internal sealed class ConventionItemScanner
{
    private const int MaxRuleFolderDepth = 3;
    private const string SkillFile = "SKILL.md";

    private static readonly FrozenDictionary<string, ConventionRuleKind> InstructionFiles = new Dictionary<string, ConventionRuleKind>(StringComparer.Ordinal)
    {
        ["AGENTS.md"] = ConventionRuleKind.AgentsFile,
        ["CLAUDE.md"] = ConventionRuleKind.ClaudeFile,
        [".cursorrules"] = ConventionRuleKind.CursorRules,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private readonly ConventionScanContext _context;
    private readonly ConventionFileReader _reader;

    /// <summary>Initializes a new instance of the <see cref="ConventionItemScanner"/> class.</summary>
    /// <param name="context">The scan context.</param>
    public ConventionItemScanner(ConventionScanContext context)
    {
        _context = context;
        _reader = new ConventionFileReader(context);
    }

    /// <summary>Tests whether a file name is an instruction file (<c>AGENTS.md</c>, <c>CLAUDE.md</c>, <c>.cursorrules</c>).</summary>
    /// <param name="name">The file name.</param>
    /// <returns><see langword="true"/> for an instruction file.</returns>
    public static bool IsInstructionFile(string name) => InstructionFiles.ContainsKey(name);

    /// <summary>Reads the skills of a <c>skills</c> folder: one <c>&lt;name&gt;/SKILL.md</c> per folder, and flat <c>&lt;name&gt;.md</c> files under <c>.autonogrammer</c>.</summary>
    /// <param name="scopeDirectory">The nested scope folder relative to the root; empty for the root.</param>
    /// <param name="sourceDirectory">The convention directory that holds the folder, for example <c>.cursor</c>.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>A task that completes when the folder has been read.</returns>
    public async Task ScanSkillsAsync(string scopeDirectory, string sourceDirectory, CancellationToken cancellationToken)
    {
        var relative = Relative(scopeDirectory, sourceDirectory, "skills");
        var list = await ListAsync(relative, ConventionItemKind.Skill, cancellationToken).ConfigureAwait(false);
        foreach (var entry in list)
        {
            if (entry.IsDirectory)
            {
                await ReadSkillFolderAsync(scopeDirectory, sourceDirectory, relative, entry.Name, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(sourceDirectory, ConventionDirectories.Autonogrammer, StringComparison.Ordinal) && ConventionText.Stem(entry.Name, ".md") is { } flat)
            {
                await ReadSkillAsync(scopeDirectory, sourceDirectory, relative + "/" + entry.Name, flat, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Reads the rules of a <c>rules</c> folder: <c>.mdc</c> and <c>.md</c> files, in folders up to three levels deep.</summary>
    /// <param name="scopeDirectory">The nested scope folder relative to the root; empty for the root.</param>
    /// <param name="sourceDirectory">The convention directory that holds the folder.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>A task that completes when the folder has been read.</returns>
    public Task ScanRulesAsync(string scopeDirectory, string sourceDirectory, CancellationToken cancellationToken) =>
        ScanRuleFolderAsync(scopeDirectory, sourceDirectory, string.Empty, 0, cancellationToken);

    /// <summary>Reads the custom commands of a <c>commands</c> folder: one command per <c>.md</c> file.</summary>
    /// <param name="sourceDirectory">The convention directory that holds the folder.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>A task that completes when the folder has been read.</returns>
    public async Task ScanCommandsAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        var relative = Relative(string.Empty, sourceDirectory, "commands");
        foreach (var entry in await ListAsync(relative, ConventionItemKind.Command, cancellationToken).ConfigureAwait(false))
        {
            if (!entry.IsDirectory && ConventionText.Stem(entry.Name, ".md") is { } name)
            {
                await ReadCommandAsync(sourceDirectory, relative + "/" + entry.Name, name, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Reads the subagent definitions of an <c>agents</c> folder: one subagent per <c>.md</c> file.</summary>
    /// <param name="sourceDirectory">The convention directory that holds the folder.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>A task that completes when the folder has been read.</returns>
    public async Task ScanSubagentsAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        var relative = Relative(string.Empty, sourceDirectory, "agents");
        foreach (var entry in await ListAsync(relative, ConventionItemKind.Subagent, cancellationToken).ConfigureAwait(false))
        {
            if (!entry.IsDirectory && ConventionText.Stem(entry.Name, ".md") is { } stem)
            {
                await ReadSubagentAsync(sourceDirectory, relative + "/" + entry.Name, stem, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Reads an instruction file (<c>AGENTS.md</c>, <c>CLAUDE.md</c>, <c>.cursorrules</c>) as an always-applied rule.</summary>
    /// <param name="scopeDirectory">The nested scope folder relative to the root; empty for the root.</param>
    /// <param name="sourceDirectory">The convention directory that holds the file, or an empty string for a file at the scope itself.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the file has been read.</returns>
    public async Task ReadInstructionFileAsync(string scopeDirectory, string sourceDirectory, string fileName, CancellationToken cancellationToken)
    {
        var inScope = sourceDirectory.Length == 0 ? fileName : sourceDirectory + "/" + fileName;
        var relative = ConventionPath.JoinRelative(scopeDirectory, inScope);
        var text = await _reader.ReadTextAsync(PathOf(relative), relative, ConventionItemKind.Rule, cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return;
        }

        _context.Collector.Add(new ConventionRule(
            inScope,
            InstructionFiles[fileName],
            ConventionText.FirstLine(text),
            [],
            AlwaysApply: true,
            _context.Scope,
            scopeDirectory,
            sourceDirectory,
            PathOf(relative),
            _context.OriginPrefix + relative));
    }

    /// <summary>Lists a hooks or MCP file that exists, without reading it.</summary>
    /// <param name="sourceDirectory">The convention directory that holds the file, or an empty string for a file at the root itself.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="isHooks"><see langword="true"/> for <c>hooks.json</c>, <see langword="false"/> for an MCP file.</param>
    public void ListConfigFile(string sourceDirectory, string fileName, bool isHooks)
    {
        var relative = sourceDirectory.Length == 0 ? fileName : sourceDirectory + "/" + fileName;
        var file = new ConventionConfigFile(_context.Scope, sourceDirectory, PathOf(relative), _context.OriginPrefix + relative);
        if (isHooks)
        {
            _context.Collector.AddHookFile(file);
        }
        else
        {
            _context.Collector.AddMcpFile(file);
        }
    }

    private static string Relative(string scopeDirectory, string sourceDirectory, string folder) =>
        ConventionPath.JoinRelative(ConventionPath.JoinRelative(scopeDirectory, sourceDirectory), folder);

    private string PathOf(string relative) => ConventionPath.Join(_context.BasePath, relative);

    private async Task<IReadOnlyList<ConventionEntry>> ListAsync(string relative, ConventionItemKind item, CancellationToken cancellationToken)
    {
        var result = await _context.Source.ListAsync(PathOf(relative), _context.Options.MaxEntriesPerDirectory, cancellationToken).ConfigureAwait(false);
        if (result.Problem is not null)
        {
            _reader.Report(item, relative, result.ProblemKind, result.Problem);
        }

        return result.Entries;
    }

    private async Task ReadSkillFolderAsync(string scopeDirectory, string sourceDirectory, string skillsRelative, string folder, CancellationToken cancellationToken)
    {
        var folderRelative = skillsRelative + "/" + folder;
        var inside = await _context.Source.ListAsync(PathOf(folderRelative), _context.Options.MaxEntriesPerDirectory, cancellationToken).ConfigureAwait(false);
        if (inside.Entries.Any(static entry => !entry.IsDirectory && string.Equals(entry.Name, SkillFile, StringComparison.Ordinal)))
        {
            await ReadSkillAsync(scopeDirectory, sourceDirectory, folderRelative + "/" + SkillFile, folder, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReadSkillAsync(string scopeDirectory, string sourceDirectory, string relative, string name, CancellationToken cancellationToken)
    {
        if (!ConventionText.IsUsableName(name))
        {
            _reader.Report(ConventionItemKind.Skill, relative, ConventionDiagnosticKind.Invalid, "the skill name '" + name + "' cannot be typed after a slash");
            return;
        }

        var file = await _reader.ReadMarkdownAsync(PathOf(relative), relative, ConventionItemKind.Skill, cancellationToken).ConfigureAwait(false);
        if (file is null)
        {
            return;
        }

        if (file.ReadText("name") is { } declared && !string.Equals(declared, name, StringComparison.Ordinal))
        {
            _reader.Report(ConventionItemKind.Skill, relative, ConventionDiagnosticKind.Invalid, "the skill declares the name '" + declared + "', which does not match its folder; the folder name is used");
        }

        _context.Collector.Add(new ConventionSkill(
            name,
            ConventionText.Describe(file),
            file.ReadList("paths"),
            file.ReadFlag("disable-model-invocation") ?? false,
            file.ReadFlag("user-invocable") ?? true,
            file.ReadText("icon"),
            file.ReadText("color"),
            _context.Scope,
            scopeDirectory,
            sourceDirectory,
            PathOf(relative),
            _context.OriginPrefix + relative));
    }

    private async Task ScanRuleFolderAsync(string scopeDirectory, string sourceDirectory, string inner, int depth, CancellationToken cancellationToken)
    {
        var relativeFolder = ConventionPath.JoinRelative(Relative(scopeDirectory, sourceDirectory, "rules"), inner);
        foreach (var entry in await ListAsync(relativeFolder, ConventionItemKind.Rule, cancellationToken).ConfigureAwait(false))
        {
            var name = ConventionPath.JoinRelative(inner, entry.Name);
            if (entry.IsDirectory && !entry.IsSymbolicLink)
            {
                if (depth < MaxRuleFolderDepth)
                {
                    await ScanRuleFolderAsync(scopeDirectory, sourceDirectory, name, depth + 1, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (!entry.IsDirectory)
            {
                await ReadRuleAsync(scopeDirectory, sourceDirectory, relativeFolder + "/" + entry.Name, name, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ReadRuleAsync(string scopeDirectory, string sourceDirectory, string relative, string nameWithExtension, CancellationToken cancellationToken)
    {
        var mdc = ConventionText.Stem(nameWithExtension, ".mdc");
        var name = mdc ?? ConventionText.Stem(nameWithExtension, ".md");
        if (name is null)
        {
            return;
        }

        var file = await _reader.ReadMarkdownAsync(PathOf(relative), relative, ConventionItemKind.Rule, cancellationToken).ConfigureAwait(false);
        if (file is null)
        {
            return;
        }

        _context.Collector.Add(new ConventionRule(
            name,
            mdc is null ? ConventionRuleKind.Markdown : ConventionRuleKind.Mdc,
            ConventionText.Describe(file),
            file.ReadList("globs"),
            file.ReadFlag("alwaysApply") ?? false,
            _context.Scope,
            scopeDirectory,
            sourceDirectory,
            PathOf(relative),
            _context.OriginPrefix + relative));
    }

    private async Task ReadCommandAsync(string sourceDirectory, string relative, string name, CancellationToken cancellationToken)
    {
        if (!ConventionText.IsUsableName(name))
        {
            _reader.Report(ConventionItemKind.Command, relative, ConventionDiagnosticKind.Invalid, "the command name '" + name + "' cannot be typed after a slash");
            return;
        }

        var file = await _reader.ReadMarkdownAsync(PathOf(relative), relative, ConventionItemKind.Command, cancellationToken).ConfigureAwait(false);
        if (file is null)
        {
            return;
        }

        _context.Collector.Add(new ConventionCommand(
            name,
            ConventionText.Describe(file),
            file.ReadText("argument-hint") ?? file.ReadText("argumentHint"),
            file.Body,
            _context.Scope,
            sourceDirectory,
            PathOf(relative),
            _context.OriginPrefix + relative));
    }

    private async Task ReadSubagentAsync(string sourceDirectory, string relative, string stem, CancellationToken cancellationToken)
    {
        var file = await _reader.ReadMarkdownAsync(PathOf(relative), relative, ConventionItemKind.Subagent, cancellationToken).ConfigureAwait(false);
        if (file is null)
        {
            return;
        }

        var name = file.ReadText("name") ?? stem;
        if (!ConventionText.IsUsableName(name))
        {
            _reader.Report(ConventionItemKind.Subagent, relative, ConventionDiagnosticKind.Invalid, "the subagent name '" + name + "' cannot be typed after a slash");
            return;
        }

        _context.Collector.Add(new ConventionSubagent(
            name,
            ConventionText.Describe(file),
            file.ReadText("model"),
            file.ReadFlag("readonly") ?? false,
            file.ReadFlag("is_background") ?? false,
            file.Body,
            _context.Scope,
            sourceDirectory,
            PathOf(relative),
            _context.OriginPrefix + relative));
    }
}
