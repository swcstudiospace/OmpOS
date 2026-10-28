namespace Autonogrammer.Presentation.Settings.Customize;

/// <summary>Reads the skill files the workspace holds. A head maps the workspace catalogue (the <c>.autonogrammer/skills</c> folder) to entries.</summary>
public interface ISkillsSettingsSource
{
    /// <summary>Reads the skills.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The catalogue.</returns>
    /// <exception cref="SettingsSourceException">The skills could not be read.</exception>
    Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken);
}
