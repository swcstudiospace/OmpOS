using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>After a save the ssh row keeps its test outcome only while every setting the connection test uses is unchanged.</summary>
public sealed class SshOutcomeCarryOverRegressionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static EnvironmentProfile Ssh() => new()
    {
        Id = "a",
        DisplayName = "Box a",
        Kind = EnvironmentKind.Ssh,
        Ssh = new SshProfile { Alias = "alpha", RemoteWorkspaceRoot = "/home/dev/work" },
    };

    /// <summary>Changing the identity file the connection uses drops the outcome of the test made with the old one.</summary>
    [Fact]
    public async Task AnIdentityFileChangeDropsTheOutcome()
    {
        await AfterAnEditDropsTheOutcomeAsync(editor => editor.IdentityFile = "/home/dev/.ssh/work");
    }

    /// <summary>Changing the jump host drops the outcome of the test made without it.</summary>
    [Fact]
    public async Task AProxyJumpChangeDropsTheOutcome()
    {
        await AfterAnEditDropsTheOutcomeAsync(editor => editor.ProxyJump = "bastion");
    }

    /// <summary>Forwarding the agent changes what the connection does, so it drops the outcome.</summary>
    [Fact]
    public async Task AForwardAgentChangeDropsTheOutcome()
    {
        await AfterAnEditDropsTheOutcomeAsync(editor => editor.ForwardAgent = true);
    }

    /// <summary>The keep-alive interval is an ssh setting the connection uses, so changing it drops the outcome.</summary>
    [Fact]
    public async Task AKeepAliveChangeDropsTheOutcome()
    {
        await AfterAnEditDropsTheOutcomeAsync(editor => editor.KeepAliveText = "30");
    }

    /// <summary>A change that the connection test does not depend on, such as the display name, keeps the outcome.</summary>
    [Fact]
    public async Task ADisplayNameChangeKeepsTheOutcome()
    {
        var page = await TestedPageAsync();
        page.StartEdit(page.Rows[0]);
        page.Editor!.DisplayName = "Renamed box";

        (await page.SaveEditAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        page.Rows[0].Status.ShouldBe(RemoteEnvironmentStatus.Ready);
    }

    private static async Task AfterAnEditDropsTheOutcomeAsync(Action<RemoteProfileEditor> change)
    {
        var page = await TestedPageAsync().ConfigureAwait(true);
        page.StartEdit(page.Rows[0]);
        change(page.Editor!);

        (await page.SaveEditAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        page.Rows[0].Status.ShouldBe(RemoteEnvironmentStatus.NotChecked);
    }

    private static async Task<RemoteEnvironmentsViewModel> TestedPageAsync()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Ssh());
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);
        await page.TestConnectionAsync(page.Rows[0], Token).ConfigureAwait(true);
        page.Rows[0].Status.ShouldBe(RemoteEnvironmentStatus.Ready);
        return page;
    }
}
