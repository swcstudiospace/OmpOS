using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Modes;
using Autonogrammer.Workspace.Conventions;

namespace Autonogrammer.Native.Composition.Checkpoints;

/// <summary>
/// Offers the user-invocable skills of the workspace conventions (<c>.cursor/skills</c>, <c>.claude/skills</c> and the other convention folders) to custom modes,
/// and reads a skill's instructions without its frontmatter.
/// </summary>
internal sealed class ConventionModeSkillSource : IModeSkillSource
{
    private const int MaxBodyBytes = 256 * 1024;

    private readonly IReadOnlyList<string> _roots;
    private readonly string? _userHome;
    private readonly IConventionFileSource? _files;
    private readonly Lock _gate = new();
    private Dictionary<string, SkillFile> _known = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ConventionModeSkillSource"/> class.</summary>
    /// <param name="roots">The workspace roots to scan.</param>
    /// <param name="userHome">The user's home directory, or <see langword="null"/> to skip user skills.</param>
    /// <param name="files">Reads the files; <see langword="null"/> reads the local disk under the roots and the home directory.</param>
    public ConventionModeSkillSource(IReadOnlyList<string> roots, string? userHome, IConventionFileSource? files = null)
    {
        ArgumentNullException.ThrowIfNull(roots);
        _roots = roots;
        _userHome = userHome;
        _files = files;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModeSkill>> ListAsync(CancellationToken cancellationToken)
    {
        var local = ConventionSources.Local(_roots, _userHome);
        var pluginRoots = Autonogrammer.Native.Composition.Plugins.BundleComposition.PluginRootsFor?.Invoke(false);
        var sources = _files is null
            ? (pluginRoots is { Count: > 0 } ? local.WithPluginRoots(pluginRoots) : local)
            : new ConventionSources(_files, _files);
        var set = await ConventionCatalog.DiscoverAsync(_roots, _userHome, sources, null, cancellationToken).ConfigureAwait(false);
        var reader = _files ?? new LocalConventionFileSource(_userHome is null ? _roots : [.. _roots, _userHome]);
        var pluginReader = sources.Plugin ?? reader;
        var files = new Dictionary<string, SkillFile>(StringComparer.Ordinal);
        var skills = new List<ModeSkill>();
        foreach (var skill in set.Skills)
        {
            if (!skill.UserInvocable || !files.TryAdd(skill.Name, new SkillFile(skill.Path, skill.Scope == ConventionScope.Plugin ? pluginReader : reader)))
            {
                continue;
            }

            var scope = skill.Scope == ConventionScope.User ? SlashScope.User : SlashScope.Project;
            skills.Add(new ModeSkill(skill.Name, skill.Description, skill.Icon, skill.Color, scope, skill.Origin));
        }

        lock (_gate)
        {
            _known = files;
        }

        return skills;
    }

    /// <inheritdoc />
    public async Task<string?> ReadBodyAsync(ModeSkill skill, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);
        SkillFile? known;
        lock (_gate)
        {
            known = _known.GetValueOrDefault(skill.Name);
        }

        if (known is null)
        {
            throw new InvalidOperationException($"The skill '{skill.Name}' is not known; list the skills first.");
        }

        var read = await known.Reader.ReadTextAsync(known.Path, MaxBodyBytes, cancellationToken).ConfigureAwait(false);
        if (!read.IsRead)
        {
            throw new InvalidOperationException($"The skill '{skill.Name}' cannot be read: {read.Problem}");
        }

        return ConventionFrontmatterParser.Parse(read.Text!).Body;
    }

    private sealed record SkillFile(string Path, IConventionFileSource Reader);
}
