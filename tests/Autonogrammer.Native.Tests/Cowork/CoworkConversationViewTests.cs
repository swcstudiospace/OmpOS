using Autonogrammer.Contracts.Controls;
using Autonogrammer.Native.Views.Cowork;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.State;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>The conversation view follows its approval queue: an approval raised after the view is bound is shown.</summary>
public sealed class CoworkConversationViewTests
{
    [Fact]
    public async Task AnApprovalRaisedAfterTheViewIsBoundIsShown()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var queue = new CoworkApprovalQueue();
            var model = new CoworkConversationModel(
                new SessionViewModel(),
                new ComposerViewModel(static _ => Task.FromResult(true), static () => Task.CompletedTask),
                queue,
                [new ProviderStatusChip("Local", ProviderChipState.Available)]);
            var view = (CoworkConversationView)ViewMapping.Create(model);
            var window = new Window { Content = view, Width = 800, Height = 640 };
            try
            {
                window.Show();
                ShellHarness.Settle(window);
                view.ApprovalsRegion.Children.ShouldBeEmpty();

                queue.Add(
                    new PendingToolApproval
                    {
                        RequestId = "req-1",
                        ToolName = "read_file",
                        RequestedAt = DateTimeOffset.UnixEpoch,
                        GrantableTools = ["read_file"],
                    },
                    "workspace/notes.md",
                    "host observed read");

                view.ApprovalsRegion.GetVisualDescendants().OfType<CoworkApprovalCard>().Count().ShouldBe(1);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }
}
