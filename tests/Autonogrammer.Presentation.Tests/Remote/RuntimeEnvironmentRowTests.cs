using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>A container or WSL row: a test that is still running when the section reloads ends on the row the section lists, never on a row left busy.</summary>
public sealed class RuntimeEnvironmentRowTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A test started before a reload finishes on the reloaded row, which is idle and can be tested again.</summary>
    [Fact]
    public async Task ATestThatFinishesAfterAReloadLeavesTheListedRowIdle()
    {
        var store = new FakeRemoteEnvironmentsHost();
        _ = store.With(Container());
        var runtime = new FakeContainerEnvironmentsHost();
        var gate = new TaskCompletionSource();
        runtime.Gate = gate.Task;
        var page = new RuntimeEnvironmentsViewModel(store, runtime, new FakeContainerLister());
        await page.LoadAsync(Token).ConfigureAwait(true);
        var started = page.TestAsync(page.Rows[0], Token);

        await page.LoadAsync(Token).ConfigureAwait(true);
        page.Rows[0].IsBusy.ShouldBeTrue();
        gate.SetResult();
        await started.ConfigureAwait(true);

        page.Rows[0].IsBusy.ShouldBeFalse();
        page.Rows[0].CanTest.ShouldBeTrue();
        page.Rows[0].EnvironmentText.ShouldBe("omp 18.12.6, Linux x86_64");
    }

    private static EnvironmentProfile Container() => new()
    {
        Id = "container-devbox",
        DisplayName = "Box devbox",
        Kind = EnvironmentKind.Container,
        Container = new ContainerProfile { Container = "devbox", RemoteUser = "dev", WorkspaceFolder = "/workspaces/app" },
    };
}
