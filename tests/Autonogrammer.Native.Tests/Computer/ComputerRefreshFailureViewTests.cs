using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Computer;
using Autonogrammer.Presentation.Computer;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Computer;

/// <summary>A permission status read the advisor cannot make is said on the Computer Use page, and a read that works clears it.</summary>
public sealed class ComputerRefreshFailureViewTests
{
    [Fact]
    public async Task AFailedPermissionStatusReadIsSaidOnThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(async () =>
        {
            var page = new ComputerUseSettingsPage(
                new SettingsFixture.FixtureGrantRegistry(),
                new SettingsFixture.FixtureDenylist(),
                new SettingsFixture.FixtureAudit(),
                new SettingsFixture.FixtureComputerSettings(),
                new SettingsFixture.FixtureKillSwitch(),
                new StatusThatFailsAfterTheFirstRead(),
                clock);
            await page.LoadAsync(ct).ConfigureAwait(true);
            var view = new ComputerSettingsPage { DataContext = page };
            var window = new Window { Width = 900, Height = 1400, Content = view };
            window.Show();
            try
            {
                SettingsTree.SettleSafe(window);
                SettingsTree.Texts(view).ShouldNotContain(ComputerPermissionAdvisor.RefreshFailed, StringComparer.Ordinal);

                page.Advisor.Open();
                clock.Timers.Single().Fire();
                SettingsTree.SettleSafe(window);

                SettingsTree.Texts(view).ShouldContain(ComputerPermissionAdvisor.RefreshFailed, StringComparer.Ordinal);
            }
            finally
            {
                window.Close();
                page.Dispose();
            }
        }, ct).ConfigureAwait(true);
    }

    private sealed class StatusThatFailsAfterTheFirstRead : IComputerStatusSource
    {
        private int _reads;

        public ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
        {
            _reads++;
            return _reads == 1
                ? ValueTask.FromResult(ComputerAvailability.Available)
                : ValueTask.FromException<ComputerAvailability>(new IOException("the status service is down"));
        }

        public ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ComputerPermissionStatus>>([]);

        public ValueTask<ComputerPermissionStatus> RequestPermissionAsync(ComputerPermissionKind kind, CancellationToken cancellationToken) =>
            ValueTask.FromException<ComputerPermissionStatus>(new NotSupportedException("The fixture does not request permissions."));
    }
}
