using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Computer;
using Autonogrammer.Presentation.Computer;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Computer;

/// <summary>A denylist entry the store refuses is said on the Computer Use page, under the denylist control.</summary>
public sealed class ComputerDenylistFailureViewTests
{
    [Fact]
    public async Task ARefusedDenylistEntryIsSaidOnThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(async () =>
        {
            var page = new ComputerUseSettingsPage(
                new SettingsFixture.FixtureGrantRegistry(),
                new RefusingDenylist(),
                new SettingsFixture.FixtureAudit(),
                new SettingsFixture.FixtureComputerSettings(),
                new SettingsFixture.FixtureKillSwitch(),
                new SettingsFixture.FixtureComputerStatus());
            await page.LoadAsync(ct).ConfigureAwait(true);
            var view = new ComputerSettingsPage { DataContext = page };
            var window = new Window { Width = 900, Height = 1400, Content = view };
            window.Show();
            try
            {
                SettingsTree.SettleSafe(window);
                SettingsTree.Texts(view).ShouldNotContain("Could not add the denylist entry: The denylist file is locked.", StringComparer.Ordinal);

                page.Model.DenylistIdentity = "com.example.app";
                page.Model.AddDenylist.Execute(null);
                SettingsTree.SettleSafe(window);

                SettingsTree.Texts(view).ShouldContain("Could not add the denylist entry: The denylist file is locked.", StringComparer.Ordinal);
            }
            finally
            {
                window.Close();
                page.Dispose();
            }
        }, ct).ConfigureAwait(true);
    }

    private sealed class RefusingDenylist : IComputerDenylistStore
    {
        public IReadOnlyList<ComputerDenylistEntry> BuiltInRows { get; } = [];

        public ValueTask AddAsync(string appIdentity, CancellationToken cancellationToken) =>
            ValueTask.FromException(new InvalidOperationException("The denylist file is locked."));
    }
}
