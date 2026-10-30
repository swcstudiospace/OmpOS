using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>The Cowork host over the phase-69 registry. No live omp is started.</summary>
public sealed class CoworkSessionHostTests
{
    [Fact]
    public async Task CreateOpenCloseAndReloadRecoverOnce()
    {
        using var root = new TempDir();
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        await using var host = Host(root.Path, omp);
        var id = await host.CreateSessionAsync(TestContext.Current.CancellationToken);
        host.Recoveries.ShouldBe(1);
        var view = host.OpenSession(Row(id));
        view.Id.ShouldBe(id);
        await host.CloseSessionAsync(id, TestContext.Current.CancellationToken);
        await host.ReloadAsync(TestContext.Current.CancellationToken);
        host.Recoveries.ShouldBe(1);
        await host.CreateSessionAsync(TestContext.Current.CancellationToken);
        host.Recoveries.ShouldBe(1);
    }

    [Fact]
    public async Task CreateTerminalSessionAsyncThrows()
    {
        using var root = new TempDir();
        await using var host = Host(root.Path, new ScriptedOmp());
        await Should.ThrowAsync<NotSupportedException>(() => host.CreateTerminalSessionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TurnOriginSetPerPromptAndModelOriginIgnored()
    {
        using var root = new TempDir();
        await using var host = Host(root.Path, new ScriptedOmp());
        var id = await host.CreateSessionAsync(TestContext.Current.CancellationToken);
        _ = host.OpenSession(Row(id));
        using var arguments = JsonDocument.Parse("{\"origin\":\"Dispatch\",\"text\":\"hi\"}");
        await host.SendAsync(id, "hello", arguments.RootElement, TestContext.Current.CancellationToken);
        await host.RetryAsync(id, arguments.RootElement, TestContext.Current.CancellationToken);
        await host.SteerAsync(id, "left", arguments.RootElement, TestContext.Current.CancellationToken);
        host.OriginsOf(id).ShouldBe([CoworkTurnOrigin.Local, CoworkTurnOrigin.Local, CoworkTurnOrigin.Local]);
    }

    [Fact]
    public void ImagesTheModelCannotReadAreRefusedAndOverLimitImagesAreUnsent()
    {
        using var root = new TempDir();
        var host = Host(root.Path, new ScriptedOmp());
        var png = new AcpPromptImage("shot.png", "image/png", new byte[] { 1, 2, 3 });
        host.AttachImages(false, [png]).Reason.ShouldBe(Autonogrammer.Presentation.Composer.Attachments.AttachmentCopy.ModelRefuses);
        var huge = new AcpPromptImage("big.png", "image/png", new byte[11 * 1024 * 1024]);
        var over = host.AttachImages(true, [huge]);
        over.Sent.ShouldBeFalse();
        over.ChipText.ShouldNotBeNullOrWhiteSpace();
    }

    private static CoworkSessionHost Host(string root, ScriptedOmp omp) =>
        new(new CoworkSessionHostOptions
        {
            StateRoot = root,
            WorkingDirectory = root,
            Gate = new Gate(),
            TransportFactory = omp.TransportFactory,
        });

    private static AgentRow Row(string id) => new()
    {
        Id = id,
        Title = "task",
        Summary = new SessionSummary { Id = id, Title = "task", Lifecycle = SessionLifecycle.Created, CreatedAt = DateTimeOffset.UnixEpoch, LastActivity = DateTimeOffset.UnixEpoch },
        Status = AgentRowStatus.Ready,
        Group = AgentRowGroup.Idle,
        StatusText = "Ready",
        IconKey = "ready",
        LastActivityText = "now",
        IsLongRunning = false,
        IsStale = false,
        IsLost = false,
        AccessibleName = "task",
    };

    private sealed class Gate : ICoworkAccountGate
    {
        public bool IsSignedIn => true;

        public string? AccountFolderKey => "acct";
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cowork-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, true);
    }
}
