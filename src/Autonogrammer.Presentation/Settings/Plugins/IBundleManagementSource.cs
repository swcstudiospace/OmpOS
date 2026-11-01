#pragma warning disable MA0048, CA1032
namespace Autonogrammer.Presentation.Settings.Plugins;

/// <summary>One installed bundle as the settings section needs it. The Native adapter is supplied by a later plan.</summary>
/// <param name="Id">The bundle id.</param>
/// <param name="Enabled">Whether the bundle is enabled.</param>
/// <param name="ProjectScope">Whether the bundle was installed for one project.</param>
/// <param name="ComponentStates">The load state of each component, already worded for the screen.</param>
/// <param name="Skills">The skills and their switches.</param>
/// <param name="Collision">The collision text when a built-in wins, or <see langword="null"/>.</param>
public sealed record BundleSnapshot(
    string Id,
    bool Enabled,
    bool ProjectScope,
    IReadOnlyList<string> ComponentStates,
    IReadOnlyList<BundleSkillSnapshot> Skills,
    string? Collision);

/// <summary>One skill switch on a bundle row.</summary>
/// <param name="Id">The skill id.</param>
/// <param name="Enabled">Whether the skill is enabled.</param>
public sealed record BundleSkillSnapshot(string Id, bool Enabled);

/// <summary>A refused install or toggle. The message is <c>BundleInvalid(code)</c> and is shown, never thrown out of the section.</summary>
public sealed class BundleManagementException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="BundleManagementException"/> class.</summary>
    /// <param name="code">The code name, for example <c>BundleTraversal</c>.</param>
    public BundleManagementException(string code)
        : base("BundleInvalid(" + code + ")")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    /// <summary>Gets the code name.</summary>
    public string Code { get; }
}

/// <summary>
/// Installs a bundle and changes enable flags. The implementation that calls the harness installer is not in this project.
/// </summary>
public interface IBundleManagementSource
{
    /// <summary>Lists installed bundles.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bundles.</returns>
    Task<IReadOnlyList<BundleSnapshot>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Installs a folder or zip.</summary>
    /// <param name="path">The folder or zip path.</param>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <returns>The installed bundle.</returns>
    /// <exception cref="BundleManagementException">The bundle was refused.</exception>
    Task<BundleSnapshot> InstallAsync(string path, CancellationToken cancellationToken);

    /// <summary>Enables or disables a bundle.</summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="enabled">The new flag.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the flag is stored.</returns>
    Task SetBundleEnabledAsync(string bundleId, bool enabled, CancellationToken cancellationToken);

    /// <summary>Enables or disables one skill.</summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="skillId">The skill id.</param>
    /// <param name="enabled">The new flag.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the flag is stored.</returns>
    Task SetSkillEnabledAsync(string bundleId, string skillId, bool enabled, CancellationToken cancellationToken);
}
