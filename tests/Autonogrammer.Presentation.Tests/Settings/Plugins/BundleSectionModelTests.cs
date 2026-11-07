#pragma warning disable MA0002, CA1001
using Autonogrammer.Presentation.Settings.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Plugins;

/// <summary>The bundle section shows rows, limit copy and refusals without throwing.</summary>
public sealed class BundleSectionModelTests
{
    /// <summary>Rows expose enable state, component states, project-scope-off and the hooks limit copy. Commands call the source.</summary>
    [Fact]
    public async Task RowsCommandsAndRefusals()
    {
        var source = new FakeSource(
        [
            new BundleSnapshot(
                "p",
                Enabled: false,
                ProjectScope: true,
                ["skills: loaded", "BundleComponentNotLoaded(hooks)"],
                [new BundleSkillSnapshot("foo", true)],
                Collision: null),
        ]);
        var model = new BundleSectionModel(source);
        await model.LoadAsync(CancellationToken.None).ConfigureAwait(true);

        model.LimitCopy.ShouldBe("hooks listed, not run");
        var row = model.Rows.Single();
        row.Enabled.ShouldBeFalse();
        row.Notice.ShouldBe(BundleSectionModel.ProjectScopeOff);
        row.ComponentStates.ShouldContain("BundleComponentNotLoaded(hooks)");

        row.ToggleCommand.Execute(null);
        await source.WaitOneAsync().ConfigureAwait(true);
        source.BundleToggles.ShouldBe([("p", true)]);

        model.Rows.Single().Skills.Single().ToggleCommand.Execute(null);
        await source.WaitOneAsync().ConfigureAwait(true);
        source.SkillToggles.ShouldBe([("p", "foo", false)]);

        model.InstallPath = "/tmp/hostile.zip";
        source.FailInstall = "BundleTraversal";
        model.InstallCommand.Execute(null);
        await source.WaitOneAsync().ConfigureAwait(true);
        model.Banner.ShouldBe("BundleInvalid(BundleTraversal)");
    }

    private sealed class FakeSource : IBundleManagementSource
    {
        private readonly List<BundleSnapshot> _rows;
        private readonly SemaphoreSlim _gate = new(0);

        public FakeSource(IReadOnlyList<BundleSnapshot> rows) => _rows = [.. rows];

        public List<(string Id, bool Enabled)> BundleToggles { get; } = [];

        public List<(string Bundle, string Skill, bool Enabled)> SkillToggles { get; } = [];

        public string? FailInstall { get; set; }

        public Task WaitOneAsync() => _gate.WaitAsync();

        public Task<IReadOnlyList<BundleSnapshot>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BundleSnapshot>>(_rows);

        public Task<BundleSnapshot> InstallAsync(string path, CancellationToken cancellationToken)
        {
            _gate.Release();
            if (FailInstall is not null)
            {
                throw new BundleManagementException(FailInstall);
            }

            return Task.FromResult(_rows[0]);
        }

        public Task SetBundleEnabledAsync(string bundleId, bool enabled, CancellationToken cancellationToken)
        {
            BundleToggles.Add((bundleId, enabled));
            _gate.Release();
            return Task.CompletedTask;
        }

        public Task SetSkillEnabledAsync(string bundleId, string skillId, bool enabled, CancellationToken cancellationToken)
        {
            SkillToggles.Add((bundleId, skillId, enabled));
            _gate.Release();
            return Task.CompletedTask;
        }
    }
}
