#pragma warning disable MA0048
using System.Windows.Input;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Settings.Plugins;

/// <summary>One skill switch on a bundle row.</summary>
public sealed class BundleSkillRowModel : ObservableModel
{
    private bool _enabled;

    /// <summary>Initializes a new instance of the <see cref="BundleSkillRowModel"/> class.</summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="id">The skill id.</param>
    /// <param name="enabled">Whether the skill is enabled.</param>
    /// <param name="toggle">The command that stores the new flag. Its parameter is unused; the row calls the source itself.</param>
    public BundleSkillRowModel(string bundleId, string id, bool enabled, ICommand toggle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(toggle);
        BundleId = bundleId;
        Id = id;
        _enabled = enabled;
        ToggleCommand = toggle;
        SwitchName = "Skill " + id + " in " + bundleId;
    }

    /// <summary>Gets the bundle id.</summary>
    public string BundleId { get; }

    /// <summary>Gets the skill id.</summary>
    public string Id { get; }

    /// <summary>Gets a value indicating whether the skill is enabled.</summary>
    public bool Enabled => _enabled;

    /// <summary>Gets the accessible name of the switch.</summary>
    public string SwitchName { get; }

    /// <summary>Gets the command that toggles the skill.</summary>
    public ICommand ToggleCommand { get; }

    internal void SetEnabled(bool enabled) => SetProperty(ref _enabled, enabled);
}

/// <summary>One installed bundle on the settings section.</summary>
public sealed class BundleRowModel : ObservableModel
{
    private bool _enabled;
    private string _notice;

    /// <summary>Initializes a new instance of the <see cref="BundleRowModel"/> class.</summary>
    /// <param name="snapshot">The bundle.</param>
    /// <param name="toggle">The command that toggles the bundle.</param>
    /// <param name="skills">The skill rows.</param>
    public BundleRowModel(BundleSnapshot snapshot, ICommand toggle, IReadOnlyList<BundleSkillRowModel> skills)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(toggle);
        ArgumentNullException.ThrowIfNull(skills);
        Id = snapshot.Id;
        ProjectScope = snapshot.ProjectScope;
        ComponentStates = snapshot.ComponentStates;
        Collision = snapshot.Collision;
        _enabled = snapshot.Enabled;
        _notice = NoticeOf(snapshot.ProjectScope, snapshot.Enabled, snapshot.Collision);
        ToggleCommand = toggle;
        Skills = skills;
        EnableName = "Enable bundle " + snapshot.Id;
    }

    /// <summary>Gets the bundle id.</summary>
    public string Id { get; }

    /// <summary>Gets a value indicating whether the bundle is project scoped.</summary>
    public bool ProjectScope { get; }

    /// <summary>Gets a value indicating whether the bundle is enabled.</summary>
    public bool Enabled => _enabled;

    /// <summary>Gets the component load states.</summary>
    public IReadOnlyList<string> ComponentStates { get; }

    /// <summary>Gets the collision text, or <see langword="null"/>.</summary>
    public string? Collision { get; }

    /// <summary>Gets the notice: a collision, <c>BundleProjectScopeOff</c>, or empty.</summary>
    public string Notice => _notice;

    /// <summary>Gets the skill switches.</summary>
    public IReadOnlyList<BundleSkillRowModel> Skills { get; }

    /// <summary>Gets the command that toggles the bundle.</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>Gets the accessible name of the enable switch.</summary>
    public string EnableName { get; }

    internal void Apply(bool enabled)
    {
        SetProperty(ref _enabled, enabled);
        var notice = NoticeOf(ProjectScope, enabled, Collision);
        SetProperty(ref _notice, notice, nameof(Notice));
    }

    private static string NoticeOf(bool projectScope, bool enabled, string? collision)
    {
        if (!string.IsNullOrEmpty(collision))
        {
            return collision;
        }

        return projectScope && !enabled ? BundleSectionModel.ProjectScopeOff : string.Empty;
    }
}
