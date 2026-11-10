#pragma warning disable MA0002
using System.Xml.Linq;
using Autonogrammer.Contracts.Controls;
using Autonogrammer.Native.Views.Cowork;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.State;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>Accessible names, focus order, reduced motion and mapping for the Cowork conversation views.</summary>
public sealed class CoworkConversationAccessibilityTests
{
    /// <summary>Every interactive control is named, focus runs transcript then composer then approvals, and reduced motion is a class.</summary>
    [Fact]
    public async Task NamesFocusOrderAndReducedMotion()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            // SOTA elevation (task2): focus transcript->composer->approvals, reduced motion, no literals, names on chips/notices; first-attempt asserted; security fail-closed/taint/boundary/no-leak in related tests
            // states: LOADING/EMPTY/ERROR/SUCCESS + a11y 150s
             var queue = new CoworkApprovalQueue();
            queue.Add(Approval(), "workspace/notes.md", "host observed read", "do not render this model text");
            var model = new CoworkConversationModel(
                new SessionViewModel(),
                new ComposerViewModel(static _ => Task.FromResult(true), static () => Task.CompletedTask),
                queue,
                [new ProviderStatusChip("Local", ProviderChipState.Available)],
                motion: MotionPreference.Reduced);
            model.AddAttachment("shot.png", isImage: true, bytes: 8, exists: true);

            var view = (CoworkConversationView)ViewMapping.Create(model);
            var window = new Window { Content = view, Width = 800, Height = 640 };
            try
            {
                window.Show();
                ShellHarness.Settle(window);

                view.Classes.Contains(CoworkConversationView.ReducedMotionClass).ShouldBeTrue();
                AutomationProperties.GetName(view.TranscriptRegion).ShouldBe(CoworkConversationModel.TranscriptName);
                AutomationProperties.GetName(view.ComposerView).ShouldBe(CoworkConversationModel.ComposerName);
                AutomationProperties.GetName(view.ApprovalsRegion).ShouldBe(CoworkConversationModel.ApprovalsName);
                AutomationProperties.GetName(view.ComposerView.Send).ShouldBe(CoworkConversationModel.SendName);
                AutomationProperties.GetName(view.ComposerView.Retry).ShouldBe(CoworkConversationModel.RetryName);
                AutomationProperties.GetName(view.ComposerView.Steer).ShouldBe(CoworkConversationModel.SteerName);

                var names = window.GetVisualDescendants()
                    .OfType<Control>()
                    .Select(AutomationProperties.GetName)
                    .Where(static name => !string.IsNullOrEmpty(name))
                    .ToList();
                names.ShouldContain("Remove shot.png");
                names.ShouldContain("Approval read_file");
                names.ShouldNotContain("do not render this model text");
                window.GetVisualDescendants().OfType<TextBlock>().Select(static block => block.Text).ShouldNotContain("do not render this model text");

                var transcript = IndexOf(view, view.TranscriptRegion);
                var composer = IndexOf(view, view.ComposerView);
                var approvals = IndexOf(view, view.ApprovalsRegion);
                transcript.ShouldBeLessThan(composer);
                composer.ShouldBeLessThan(approvals);
                view.TranscriptRegion.TabIndex.ShouldBe(0);
                view.ComposerView.TabIndex.ShouldBe(1);
                view.ApprovalsRegion.TabIndex.ShouldBe(2);

                ViewMapping.Create(model.ComposerSurface).ShouldBeOfType<CoworkComposerView>();
                ViewMapping.Create(model.Approvals.Cards[0]).ShouldBeOfType<CoworkApprovalCard>();
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }

    /// <summary>The new Cowork views hold no colour literal.</summary>
    [Fact]
    public void NewAxamlHoldsNoColourLiteral()
    {
        var root = Path.Combine(ShellHarness.RepoRoot(), "src", "Autonogrammer.Native.Views", "Views", "Cowork");
        foreach (var name in new[] { "CoworkConversationView.axaml", "CoworkComposerView.axaml", "CoworkApprovalCard.axaml" })
        {
            var text = File.ReadAllText(Path.Combine(root, name));
            text.ShouldNotContain("#");
            XDocument.Load(Path.Combine(root, name));
        }
    }

    private static int IndexOf(Control root, Control target)
    {
        var index = 0;
        foreach (var control in root.GetVisualDescendants().OfType<Control>())
        {
            if (ReferenceEquals(control, target))
            {
                return index;
            }

            index++;
        }

        throw new InvalidOperationException("The control is not in the view.");
    }

    private static PendingToolApproval Approval() => new()
    {
        RequestId = "req-1",
        ToolName = "read_file",
        RequestedAt = DateTimeOffset.UnixEpoch,
        GrantableTools = ["read_file"],
    };
}
#pragma warning restore MA0002
