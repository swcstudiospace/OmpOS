using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Tests.Parity;
using Autonogrammer.Native.Views.Cowork.Artifacts;
using Autonogrammer.Presentation.Cowork.Artifacts;
using Avalonia.Threading;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// An artifact card that reaches the session stream. The runtime hands it to the artifact panel and keeps it out of the transcript, the panel's
/// view shows the delivered version whether it was bound before the delivery or after it, and a successful artifact_create publishes its card
/// through the session stream so the panel opens on the stored version.
/// </summary>
public sealed class ArtifactBlockDeliveryTests
{
    private const string Csv = "name,total\nwidget,4\n";
    private static readonly string Sha = new('a', 64);

    /// <summary>A card on the session stream reaches the panel, opens it on the version, and never enters the transcript.</summary>
    [Fact]
    public async Task AnArtifactCardOnTheStreamReachesThePanelAndNotTheTranscript()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var card = Card();
            var panel = Panel();
            var channel = new ScriptedPromptChannel(new ScriptedTurn([new UiBlockEvent(card), new CompletedEvent("end_turn")]));
            var runtime = AgentSessionRuntimeTests.Create(channel, artifactDelivered: (block, token) => DeliverFromStoreAsync(panel, block, token));
            await using var runtimeScope = runtime.ConfigureAwait(true);

            (await runtime.Composer.SendFromAsync("make a table").ConfigureAwait(true)).ShouldBeTrue();
            await ShellWait.UntilAsync(() => panel.Delivered is not null, "the card to reach the panel", ct).ConfigureAwait(true);
            await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming, "the run to end", ct).ConfigureAwait(true);

            panel.IsOpen.ShouldBeTrue();
            panel.Delivered!.Item.ArtifactId.ShouldBe(card.ArtifactId);
            panel.Delivered.Item.Number.ShouldBe(card.Version);
            runtime.Session.Tree.Blocks.ShouldAllBe(block => block is UserMessageBlock);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A successful artifact_create publishes its card through the session stream, and the panel opens on the stored version.</summary>
    [Fact]
    public async Task ASuccessfulArtifactCreatePublishesItsCardThroughTheSessionStream()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "autonogrammer-artifact-card-tests", Guid.NewGuid().ToString("N"));
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var composition = new ArtifactsComposition(new SignedInGate(CoworkAccountFolder.KeyFromSubject("account-1")), root, null, null, null);
            var channel = new ScriptedPromptChannel(new ScriptedTurn([new CompletedEvent("end_turn")]));
            var runtime = AgentSessionRuntimeTests.Create(channel, artifactDelivered: composition.DeliverAsync, sessionId: "session-1");
            await using var runtimeScope = runtime.ConfigureAwait(true);
            composition.CardSink = (sessionId, card) =>
            {
                if (string.Equals(sessionId, runtime.SessionId, StringComparison.Ordinal))
                {
                    Dispatcher.UIThread.Post(() => runtime.ShowArtifactCard(card));
                }
            };

            var context = new CoworkSessionContext
            {
                SessionId = "session-1",
                WorkingDirectory = root,
                TrustedFolders = [root],
                ModelReadsImages = false,
            };
            var arguments = JsonDocument.Parse("""{"kind":"Csv","title":"Quarterly","content":"name,total\nwidget,4\n"}""").RootElement;
            var invocation = new CoworkToolInvocation("session-1", "call-1", "artifact_create", arguments, CoworkTurnOrigin.Local);

            var result = await composition.ToolProvider.InvokeAsync(context, invocation, ct).ConfigureAwait(true);

            result.IsError.ShouldBeFalse();
            await ShellWait.UntilAsync(() => composition.Panel.Delivered is not null, "the created card to reach the panel", ct).ConfigureAwait(true);
            composition.Panel.IsOpen.ShouldBeTrue();
            composition.Panel.Delivered!.Item.Title.ShouldBe("Quarterly");
            composition.Panel.Delivered.Item.Number.ShouldBe(1);
            composition.Panel.Delivered.Item.ArtifactId.ShouldNotBeNullOrEmpty();
            runtime.Session.Tree.Blocks.ShouldBeEmpty();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A view bound before the delivery shows the delivered table.</summary>
    [Fact]
    public async Task AViewBoundBeforeTheDeliveryShowsTheTable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var panel = Panel();
            var view = (ArtifactPanelView)ViewMapping.Create(panel);

            await panel.DeliverAsync(Delivery(), ct).ConfigureAwait(true);

            AssertTable(view);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A view bound after the delivery shows the delivered table, as a dock that recreates its view does.</summary>
    [Fact]
    public async Task AViewBoundAfterTheDeliveryShowsTheTable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var panel = Panel();
            await panel.DeliverAsync(Delivery(), ct).ConfigureAwait(true);

            var view = (ArtifactPanelView)ViewMapping.Create(panel);

            AssertTable(view);
        }, ct).ConfigureAwait(true);
    }

    private static void AssertTable(ArtifactPanelView view)
    {
        var table = view.FrameContentHost.Content.ShouldBeOfType<ArtifactTableView>();
        table.CaptionText.ShouldBe("Quarterly: 1 rows, 2 columns");
    }

    private static ArtifactPanelModel Panel() =>
        new(new NativeWebViewArtifactSurface(static _ => ArtifactDocumentResult.Refused(ArtifactSurfaceReason.DocumentRefused)), new ArtifactPanelOptions(AutoOpen: false));

    private static ArtifactBlock Card() => new()
    {
        Id = "block-artifact-1",
        ArtifactId = "artifact-1",
        Version = 2,
        Title = "Quarterly",
        ArtifactKind = ArtifactKind.Csv,
        Action = ArtifactAction.Updated,
    };

    private static ArtifactPanelDelivery Delivery(string artifactId = "artifact-1", int number = 2) =>
        new(new ArtifactPanelItem(artifactId, "Quarterly", ArtifactKind.Csv, number, number, Sha, []), Encoding.UTF8.GetBytes(Csv), null, null);

    private static async Task<bool> DeliverFromStoreAsync(ArtifactPanelModel panel, ArtifactBlock block, CancellationToken cancellationToken)
    {
        await panel.DeliverAsync(Delivery(block.ArtifactId, block.Version), cancellationToken).ConfigureAwait(true);
        return true;
    }

    private sealed class SignedInGate(string key) : ICoworkAccountGate
    {
        public bool IsSignedIn => true;

        public string? AccountFolderKey => key;
    }
}
