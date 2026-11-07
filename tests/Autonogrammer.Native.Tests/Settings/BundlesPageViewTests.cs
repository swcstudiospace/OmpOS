#pragma warning disable MA0002
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings.Plugins;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The bundle section view draws the install box, rows, switches and limit copy without colour literals.</summary>
public sealed class BundlesPageViewTests
{
    /// <summary>The view shows one row, the enable switch, the skill switch, component states and the limit copy, each named.</summary>
    [Fact]
    public async Task RendersInstallBoxRowsAndLimitCopy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(async () =>
        {
            var model = new BundleSectionModel(new IdleSource());
            await model.LoadAsync(ct).ConfigureAwait(true);
            var view = new BundlesPageView { DataContext = model };
            var window = new Window { Width = 900, Height = 1200, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);
            try
            {
                var texts = SettingsTree.Texts(view);
                texts.ShouldContain("hooks listed, not run");
                texts.ShouldContain("p");
                texts.ShouldContain(BundleSectionModel.ProjectScopeOff);
                texts.ShouldContain("BundleComponentNotLoaded(hooks)");
                SettingsTree.Has<TextBox>(view, "Bundle folder or zip path").ShouldBeTrue();
                SettingsTree.Has<Button>(view, "Install bundle").ShouldBeTrue();
                SettingsTree.Has<ToggleSwitch>(view, "Enable bundle p").ShouldBeTrue();
                SettingsTree.Has<ToggleSwitch>(view, "Skill foo in p").ShouldBeTrue();
                SettingsTree.Unnamed(view).ShouldBeEmpty();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    private sealed class IdleSource : IBundleManagementSource
    {
        public Task<IReadOnlyList<BundleSnapshot>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BundleSnapshot>>(
            [
                new BundleSnapshot(
                    "p",
                    false,
                    true,
                    ["BundleComponentNotLoaded(hooks)"],
                    [new BundleSkillSnapshot("foo", true)],
                    null),
            ]);

        public Task<BundleSnapshot> InstallAsync(string path, CancellationToken cancellationToken) =>
            throw new BundleManagementException("BundleTraversal");

        public Task SetBundleEnabledAsync(string bundleId, bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetSkillEnabledAsync(string bundleId, string skillId, bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

#pragma warning restore MA0002
