using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Plugins;

/// <summary>A bundle notice is a sentence a user can read, and it comes from the settings copy, never an internal identifier.</summary>
public sealed class BundleNoticeCopyRegressionTests
{
    /// <summary>A disabled project-scope bundle shows the settings sentence, not the identifier that names the condition.</summary>
    [Fact]
    public async Task ProjectScopeOffNoticeIsAUserSentenceFromSettingsCopy()
    {
        var model = new BundleSectionModel(new IdleSource());
        await model.LoadAsync(CancellationToken.None).ConfigureAwait(true);

        var notice = model.Rows.Single().Notice;

        notice.ShouldBe(SettingsCopy.BundleProjectScopeOff, StringComparer.Ordinal);
        notice.ShouldNotBe("BundleProjectScopeOff", StringComparer.Ordinal);
        notice.ShouldContain(" ", Case.Sensitive);
        notice.ShouldEndWith(".", Case.Sensitive);
    }

    private sealed class IdleSource : IBundleManagementSource
    {
        public Task<IReadOnlyList<BundleSnapshot>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BundleSnapshot>>(
            [
                new BundleSnapshot("p", Enabled: false, ProjectScope: true, [], [], Collision: null),
            ]);

        public Task<BundleSnapshot> InstallAsync(string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The notice test never installs a bundle.");

        public Task SetBundleEnabledAsync(string bundleId, bool enabled, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The notice test never toggles a bundle.");

        public Task SetSkillEnabledAsync(string bundleId, string skillId, bool enabled, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The notice test never toggles a skill.");
    }
}
