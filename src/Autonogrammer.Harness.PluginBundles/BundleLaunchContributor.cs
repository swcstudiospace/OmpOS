using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.PluginBundles;

/// <summary>
/// Contributes enabled bundle skill roots as <c>skills.customDirectories</c> overlay entries and a <c>--skills</c> filter for disabled skills.
/// It contributes no environment and no process wrapper. Hooks are never executed.
/// </summary>
public sealed class BundleLaunchContributor : ILaunchContributor
{
    /// <summary>The overlay key, including the equals sign. The value is the absolute skills directory.</summary>
    public const string OverlayPrefix = "skills.customDirectories=";

    private readonly IReadOnlyList<string> _installRoots;
    private readonly SkillStateStore _skills;
    private readonly BundleStateStore _bundles;
    private readonly IReadOnlySet<string> _reservedSkills;

    /// <summary>Initializes a new instance of the <see cref="BundleLaunchContributor"/> class.</summary>
    /// <param name="installRoots">Directories that hold installed bundles.</param>
    /// <param name="skills">The skill enable store.</param>
    /// <param name="bundles">The bundle enable store.</param>
    /// <param name="reservedSkills">Built-in skill ids. A colliding bundle skill is not contributed.</param>
    public BundleLaunchContributor(
        IReadOnlyList<string> installRoots,
        SkillStateStore skills,
        BundleStateStore bundles,
        IReadOnlySet<string>? reservedSkills = null)
    {
        ArgumentNullException.ThrowIfNull(installRoots);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(bundles);
        _installRoots = installRoots;
        _skills = skills;
        _bundles = bundles;
        _reservedSkills = reservedSkills ?? new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Gets the skill roots contributed by the latest <see cref="Contribute"/> call.</summary>
    public IReadOnlyList<string> EnabledSkillRoots { get; private set; } = [];

    /// <summary>Gets disabled skills that could not be named in <c>--skills</c>. They are omitted from the overlay, never silently included.</summary>
    public IReadOnlyList<BundleSkillExclusionReport> ExclusionReports { get; private set; } = [];

    /// <summary>Gets collisions with built-in skill ids found by the latest <see cref="Contribute"/> call.</summary>
    public IReadOnlyList<string> Collisions { get; private set; } = [];

    /// <inheritdoc/>
    public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = Plan(cancellationToken);
        EnabledSkillRoots = plan.Roots;
        ExclusionReports = plan.Reports;
        Collisions = plan.Collisions;
        var arguments = new List<string>();
        if (plan.Reports.Count == 0 && plan.HasExcludableDisabled)
        {
            arguments.Add("--skills");
            arguments.Add(string.Join(',', plan.Enabled.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
        }

        return new LaunchContribution
        {
            Arguments = arguments,
            Environment = new Dictionary<string, string?>(StringComparer.Ordinal),
            OverlayEntries = plan.Overlays,
            SystemPromptParagraph = plan.Reports.Count == 0 ? string.Empty : string.Join(' ', plan.Reports.Select(static report => report.Reason)),
            FirstPromptBlock = string.Empty,
            ProcessWrapper = null,
        };
    }

    private LaunchPlan Plan(CancellationToken cancellationToken)
    {
        var overlays = new List<string>();
        var roots = new List<string>();
        var enabled = new List<string>();
        var reports = new List<BundleSkillExclusionReport>();
        var collisions = new List<string>();
        var hasDisabled = false;
        foreach (var bundle in EnumerateBundles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsBundleEnabled(bundle))
            {
                continue;
            }

            Collect(bundle, overlays, roots, enabled, reports, collisions, ref hasDisabled);
        }

        return new LaunchPlan(overlays, roots, enabled, reports, collisions, hasDisabled);
    }

    private void Collect(
        InstalledBundle bundle,
        List<string> overlays,
        List<string> roots,
        List<string> enabled,
        List<BundleSkillExclusionReport> reports,
        List<string> collisions,
        ref bool hasDisabled)
    {
        var skillsDir = Path.Combine(bundle.Directory, "skills");
        if (!Directory.Exists(skillsDir))
        {
            return;
        }

        var includeRoot = false;
        var blocked = false;
        foreach (var skillDir in Directory.EnumerateDirectories(skillsDir).Order(StringComparer.Ordinal))
        {
            var skillId = Path.GetFileName(skillDir);
            if (!File.Exists(Path.Combine(skillDir, "SKILL.md")))
            {
                continue;
            }

            if (_reservedSkills.Contains(skillId))
            {
                collisions.Add("BundleInvalid(BundleNameCollision) " + bundle.Id + "/" + skillId);
                continue;
            }

            if (!_skills.IsEnabled(bundle.Id, skillId))
            {
                if (!CanExclude(skillId))
                {
                    reports.Add(new BundleSkillExclusionReport(bundle.Id, skillId, "disabled skill '" + skillId + "' cannot be excluded"));
                    blocked = true;
                }
                else
                {
                    hasDisabled = true;
                }

                continue;
            }

            enabled.Add(skillId);
            includeRoot = true;
        }

        if (includeRoot && !blocked)
        {
            var full = Path.GetFullPath(skillsDir);
            roots.Add(full);
            overlays.Add(OverlayPrefix + full);
        }
    }

    private bool IsBundleEnabled(InstalledBundle bundle)
    {
        if (_bundles.TryGetEnabled(bundle.Id, out var enabled))
        {
            return enabled;
        }

        return BundleInstaller.ReadScope(bundle.Directory) != BundleInstallScope.Project;
    }

    private IEnumerable<InstalledBundle> EnumerateBundles()
    {
        foreach (var root in _installRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(directory);
                if (name.StartsWith('.'))
                {
                    continue;
                }

                yield return new InstalledBundle(name, directory);
            }
        }
    }

    private static bool CanExclude(string skillId)
    {
        foreach (var character in skillId)
        {
            if (character is '*' or '?' or '[' or ']' or ',')
            {
                return false;
            }
        }

        return skillId.Length > 0;
    }

    private readonly record struct InstalledBundle(string Id, string Directory);

    private readonly record struct LaunchPlan(
        IReadOnlyList<string> Overlays,
        IReadOnlyList<string> Roots,
        IReadOnlyList<string> Enabled,
        IReadOnlyList<BundleSkillExclusionReport> Reports,
        IReadOnlyList<string> Collisions,
        bool HasExcludableDisabled);
}
