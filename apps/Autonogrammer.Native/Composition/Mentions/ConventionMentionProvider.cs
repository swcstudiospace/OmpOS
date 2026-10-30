using Autonogrammer.Presentation.Mentions;
using Autonogrammer.Workspace.Conventions;

namespace Autonogrammer.Native.Composition.Mentions;

/// <summary>
/// The rules and skills a mention can attach, from <see cref="ConventionCatalog"/>. A <b>rule</b> is offered when it does not always apply: a rule with <c>alwaysApply: true</c> and every
/// instruction file (<c>AGENTS.md</c>, <c>CLAUDE.md</c>, <c>.cursorrules</c>, which always apply) is already in the agent's context, so mentioning it would only repeat it. Every other rule is offered,
/// whether it applies manually, to matching files or on the model's judgement, because the user may want to pull it in now. A <b>skill</b> is offered when it is user invocable. The discovery runs once,
/// on first use, and is kept for the provider's lifetime; <see cref="RefreshAsync"/> reads the files again. A body is read through the file source that found the item, without its front matter, and cut
/// to the byte cap on a character boundary.
/// </summary>
internal sealed class ConventionMentionProvider : IMentionConventionProvider
{
    private readonly IReadOnlyList<string> _roots;
    private readonly string? _userHome;
    private readonly ConventionDiscoveryOptions _options;
    private readonly System.Threading.Lock _gate = new();
    private ConventionSources? _sources;
    private Task<ConventionSet>? _discovery;

    /// <summary>Creates the provider. It reads nothing until it is used.</summary>
    /// <param name="roots">The workspace roots, in precedence order.</param>
    /// <param name="userHome">The user's home directory, or <see langword="null"/> to skip user conventions.</param>
    /// <param name="sources">The file trees to read, or <see langword="null"/> for the local disk (created on first use).</param>
    /// <param name="options">The bounds of the discovery, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="roots"/> is null.</exception>
    internal ConventionMentionProvider(
        IReadOnlyList<string> roots,
        string? userHome,
        ConventionSources? sources = null,
        ConventionDiscoveryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(roots);

        _roots = roots;
        _userHome = userHome;
        _sources = sources;
        _options = options ?? ConventionDiscoveryOptions.Default;
    }

    /// <summary>Drops the cached discovery, so the next listing or read finds the rules and skills on disk as they are now.</summary>
    /// <param name="cancellationToken">Cancels the wait for the new discovery.</param>
    /// <returns>A task that completes when the new discovery has finished.</returns>
    internal Task RefreshAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _discovery = null;
        }

        return DiscoverAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MentionConventionItem>> ListRulesAsync(CancellationToken cancellationToken)
    {
        var set = await DiscoverAsync(cancellationToken).ConfigureAwait(false);
        return [.. set.Rules.Where(static rule => !rule.AlwaysApply).Select(static rule => new MentionConventionItem(rule.Name, rule.Description, ScopeName(rule.Scope)))];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MentionConventionItem>> ListSkillsAsync(CancellationToken cancellationToken)
    {
        var set = await DiscoverAsync(cancellationToken).ConfigureAwait(false);
        return [.. set.Skills.Where(static skill => skill.UserInvocable).Select(static skill => new MentionConventionItem(skill.Name, skill.Description, ScopeName(skill.Scope)))];
    }

    /// <inheritdoc/>
    /// <exception cref="MentionUnavailableException">The rule or skill is gone or cannot be read, or <paramref name="kind"/> is neither rule nor skill.</exception>
    public async Task<MentionContent> ReadAsync(MentionKind kind, string name, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (kind is not (MentionKind.Rule or MentionKind.Skill))
        {
            throw new MentionUnavailableException("Only a rule or a skill can be read here.");
        }

        var set = await DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var noun = kind == MentionKind.Rule ? "rule" : "skill";
        if (Locate(set, kind, name) is not { } located)
        {
            throw new MentionUnavailableException("The " + noun + " '" + name + "' no longer exists.");
        }

        var sources = GetSources();
        var source = located.Scope == ConventionScope.Project ? sources.Project : sources.User;
        var read = await source.ReadTextAsync(located.Path, _options.MaxFileBytes, cancellationToken).ConfigureAwait(false);
        if (read.Text is not { } text)
        {
            throw new MentionUnavailableException("The " + noun + " '" + name + "' cannot be read: " + (read.Problem ?? "the file is not readable") + ".");
        }

        return MentionTextCut.Cut(ConventionFrontmatterParser.Parse(text).Body.TrimStart('\r', '\n'), maxBytes);
    }

    private static (ConventionScope Scope, string Path)? Locate(ConventionSet set, MentionKind kind, string name)
    {
        if (kind == MentionKind.Rule)
        {
            foreach (var rule in set.Rules)
            {
                if (string.Equals(rule.Name, name, StringComparison.Ordinal))
                {
                    return (rule.Scope, rule.Path);
                }
            }
        }
        else
        {
            foreach (var skill in set.Skills)
            {
                if (string.Equals(skill.Name, name, StringComparison.Ordinal))
                {
                    return (skill.Scope, skill.Path);
                }
            }
        }

        return null;
    }

    private static string ScopeName(ConventionScope scope) => scope == ConventionScope.Project ? "project" : "user";

    private ConventionSources GetSources()
    {
        lock (_gate)
        {
            return _sources ??= ConventionSources.Local(_roots, _userHome);
        }
    }

    private Task<ConventionSet> DiscoverAsync(CancellationToken cancellationToken)
    {
        Task<ConventionSet> discovery;
        lock (_gate)
        {
            if (_discovery is null || (_discovery.IsCompleted && _discovery.Status != TaskStatus.RanToCompletion))
            {
                var sources = _sources ??= ConventionSources.Local(_roots, _userHome);
                _discovery = Task.Run(() => ConventionCatalog.DiscoverAsync(_roots, _userHome, sources, _options, CancellationToken.None), CancellationToken.None);
            }

            discovery = _discovery;
        }

        return discovery.WaitAsync(cancellationToken);
    }
}
