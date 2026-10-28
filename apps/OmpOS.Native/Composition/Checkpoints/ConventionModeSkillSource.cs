using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Modes;
using OmpOS.Workspace.Conventions;

namespace OmpOS.Native.Composition.Checkpoints;

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
    private Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private IConventionFileSource? _reader;

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
        var sources = _files is null
            ? ConventionSources.Local(_roots, _userHome)
            : new ConventionSources(_files, _files);
        var set = await ConventionCatalog.DiscoverAsync(_roots, _userHome, sources, null, cancellationToken).ConfigureAwait(false);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        var skills = new List<ModeSkill>();
        foreach (var skill in set.Skills)
        {
            if (!skill.UserInvocable || !paths.TryAdd(skill.Name, skill.Path))
            {
                continue;
            }

            var scope = skill.Scope == ConventionScope.User ? SlashScope.User : SlashScope.Project;
            skills.Add(new ModeSkill(skill.Name, skill.Description, skill.Icon, skill.Color, scope, skill.Origin));
        }

        lock (_gate)
        {
            _paths = paths;
            _reader = _files ?? new LocalConventionFileSource(_userHome is null ? _roots : [.. _roots, _userHome]);
        }

        return skills;
    }

    /// <inheritdoc />
    public async Task<string?> ReadBodyAsync(ModeSkill skill, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);
        string? path;
        IConventionFileSource? reader;
        lock (_gate)
        {
            path = _paths.GetValueOrDefault(skill.Name);
            reader = _reader;
        }

        if (path is null || reader is null)
        {
            throw new InvalidOperationException($"The skill '{skill.Name}' is not known; list the skills first.");
        }

        var read = await reader.ReadTextAsync(path, MaxBodyBytes, cancellationToken).ConfigureAwait(false);
        if (!read.IsRead)
        {
            throw new InvalidOperationException($"The skill '{skill.Name}' cannot be read: {read.Problem}");
        }

        return ConventionFrontmatterParser.Parse(read.Text!).Body;
    }
}
