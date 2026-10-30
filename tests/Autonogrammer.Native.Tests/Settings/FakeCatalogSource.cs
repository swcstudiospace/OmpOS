using Autonogrammer.Presentation.Settings.Customize;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>A scripted source of the Rules, Skills or Commands page. One fake serves any of the three.</summary>
internal sealed class FakeCatalogSource : FakeSettingsSource<CustomizeCatalog>, IRulesSettingsSource, ISkillsSettingsSource, ICommandsSettingsSource
{
    /// <summary>Creates a source that lists two entries and reports one file it could not read.</summary>
    public FakeCatalogSource()
        : base(new CustomizeCatalog(
            [
                new CustomizeEntry("Be brief", CustomizeScope.Project, ".autonogrammer/rules/brief.md", "Answer in few words."),
                new CustomizeEntry("House style", CustomizeScope.User, "~/.autonogrammer/rules/style.md", string.Empty),
            ],
            []))
    {
    }

    /// <inheritdoc />
    public Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken) => LoadCoreAsync(cancellationToken);
}
