using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using OmpOS.Native.Tests.Settings;
using OmpOS.Native.Tests.Workspace;
using OmpOS.Native.Views.Checkpoints;
using OmpOS.Presentation.Checkpoints;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Checkpoints;

/// <summary>
/// The checkpoint timeline panel on the headless desktop: what it shows for each state, that a rewind is never run without the confirmation step, that Esc and Cancel
/// return focus to the row, that the live region follows a failure, and that every control is named, big enough and in keyboard order.
/// </summary>
public sealed class CheckpointsTimelineViewTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CheckpointTurnInfo Turn(int id)
    {
        var number = id.ToString(CultureInfo.InvariantCulture);
        return new CheckpointTurnInfo(id, "Prompt " + number, Now.AddMinutes(-10 * id), ["f" + number + ".txt"]);
    }

    private static CheckpointFilePreview Restore(string path) => new(path, CheckpointFileState.Restore, path + " detail");

    private static CheckpointFilePreview Stale(string path) => new(path, CheckpointFileState.Stale, path + " detail");

    private static Scene Open(int turns, params CheckpointFilePreview[] files)
    {
        var backend = new ScriptedCheckpointBackend { Turns = [.. Enumerable.Range(1, turns).Select(Turn)] };
        backend.PreviewFiles = files.Length > 0 ? files : [Restore("a.txt")];
        var model = new CheckpointsModel("s1", backend, new FixedCheckpointClock(Now));
        model.Open();
        model.RefreshCommand.Execute(null);
        var view = new CheckpointsTimelineView { DataContext = model };
        var window = ViewHost.Show(view);
        return new Scene(model, backend, view, window);
    }

    private static Button RowButton(Scene scene, string name) => SettingsTree.Named<Button>(scene.View, name);

    private static void Click(Scene scene, Button button) => SettingsTree.Click(scene.Window, button);

    private sealed record Scene(CheckpointsModel Model, ScriptedCheckpointBackend Backend, CheckpointsTimelineView View, Window Window);

    /// <summary>The panel is hidden while the model is closed; once open it lists the turns with their time and files, the count, the empty sentence before there is one, and Refresh reads again.</summary>
    [Fact]
    public async Task ThePanelHidesWhileClosedAndListsTurnsWithCountAndEmptyState()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var backend = new ScriptedCheckpointBackend { Turns = [Turn(1), Turn(2)] };
            var model = new CheckpointsModel("s1", backend, new FixedCheckpointClock(Now));
            var view = new CheckpointsTimelineView { DataContext = model };
            var window = ViewHost.Show(view);

            view.IsEffectivelyVisible.ShouldBeFalse();

            model.Open();
            ShellHarness.Settle(window);
            view.IsEffectivelyVisible.ShouldBeTrue();
            view.Empty.IsVisible.ShouldBeTrue();
            view.Empty.Text.ShouldBe(CheckpointsCopy.Empty);
            view.Count.Text.ShouldBe(CheckpointsCopy.Count(0));
            view.Rows.ItemCount.ShouldBe(0);

            SettingsTree.Click(window, view.Refresh);

            view.Rows.ItemCount.ShouldBe(2);
            view.Empty.IsVisible.ShouldBeFalse();
            view.Count.Text.ShouldBe(CheckpointsCopy.Count(2));
            view.Status.Text.ShouldBe(CheckpointsCopy.Count(2) + ".");
            var texts = SettingsTree.Texts(view);
            texts.ShouldContain("#1", StringComparer.Ordinal);
            texts.ShouldContain("Prompt 1", StringComparer.Ordinal);
            texts.ShouldContain("Prompt 2", StringComparer.Ordinal);
            texts.ShouldContain("10 min ago", StringComparer.Ordinal);
            texts.ShouldContain("20 min ago", StringComparer.Ordinal);
            texts.ShouldContain("f2.txt", StringComparer.Ordinal);

            backend.Turns = [Turn(1)];
            SettingsTree.Click(window, view.Refresh);
            view.Rows.ItemCount.ShouldBe(1);
            view.Count.Text.ShouldBe(CheckpointsCopy.Count(1));
            window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A row's Rewind button only asks: the confirmation names what is restored and says the conversation stays, focus moves to Cancel, nothing is written until Confirm, and then the rewind runs once.</summary>
    [Fact]
    public async Task ARewindNeedsTheConfirmationStepBeforeAnythingIsWritten()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var scene = Open(2, Restore("a.txt"), Restore("b.txt"));

            Click(scene, RowButton(scene, CheckpointsCopy.RewindName(2)));

            scene.Backend.Rewinds.ShouldBeEmpty();
            scene.Model.IsConfirming.ShouldBeTrue();
            scene.Model.PendingTurnId.ShouldBe(2);
            scene.View.Details.IsVisible.ShouldBeTrue();
            scene.View.Confirmation.IsVisible.ShouldBeTrue();
            scene.View.Confirmation.Text.ShouldBe(scene.Model.ConfirmationText);
            scene.View.Confirmation.Text.ShouldNotBeNull().ShouldContain("The conversation is not changed", Case.Sensitive);
            var texts = SettingsTree.Texts(scene.View);
            texts.ShouldContain("a.txt: a.txt detail", StringComparer.Ordinal);
            texts.ShouldContain("b.txt: b.txt detail", StringComparer.Ordinal);
            scene.View.Force.IsEffectivelyVisible.ShouldBeFalse();
            SettingsTree.FocusedName(scene.Window).ShouldBe(CheckpointsViewText.CancelName);

            var rewound = 0;
            scene.Model.Rewound += (_, _) => rewound++;
            Click(scene, scene.View.Confirm);

            scene.Backend.Rewinds.ShouldBe([(2, false)]);
            rewound.ShouldBe(1);
            scene.Model.IsConfirming.ShouldBeFalse();
            scene.View.Details.IsVisible.ShouldBeFalse();
            scene.View.Status.Text.ShouldNotBeNull().ShouldContain("Restored 1 file.", Case.Sensitive);
            scene.View.Status.Text.ShouldContain(CheckpointsCopy.ConversationUnchanged, Case.Sensitive);
            scene.Window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The Overwrite button is offered only when a file changed after the agent's edit; Confirm never forces, and Overwrite does.</summary>
    [Fact]
    public async Task ForceIsOfferedOnlyForStaleFilesAndConfirmNeverForces()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var plain = Open(1, Restore("a.txt"));
            Click(plain, RowButton(plain, CheckpointsCopy.RewindName(1)));
            plain.Model.CanForce.ShouldBeFalse();
            plain.View.Force.IsEffectivelyVisible.ShouldBeFalse();
            plain.Window.Close();

            var scene = Open(1, Restore("a.txt"), Stale("b.txt"));
            Click(scene, RowButton(scene, CheckpointsCopy.RewindName(1)));
            scene.View.Force.IsEffectivelyVisible.ShouldBeTrue();
            scene.View.Force.Content.ShouldBe(CheckpointsCopy.ForceLabel);
            AutomationProperties.GetName(scene.View.Force).ShouldBe(CheckpointsCopy.ForceLabel);

            Click(scene, scene.View.Confirm);
            scene.Backend.Rewinds.ShouldBe([(1, false)]);

            Click(scene, RowButton(scene, CheckpointsCopy.RewindName(1)));
            scene.View.Force.IsEffectivelyVisible.ShouldBeTrue();
            Click(scene, scene.View.Force);
            scene.Backend.Rewinds.ShouldBe([(1, false), (1, true)]);
            scene.Window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Esc cancels a pending confirmation and puts focus back on the row's Rewind button, as the Cancel button does; a second Esc closes the panel and raises the close request. Nothing is written.</summary>
    [Fact]
    public async Task EscapeCancelsTheConfirmationFirstAndThenClosesThePanel()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var scene = Open(2);
            var closed = 0;
            scene.View.CloseRequested += (_, _) => closed++;

            Click(scene, RowButton(scene, CheckpointsCopy.RewindName(1)));
            scene.Model.IsConfirming.ShouldBeTrue();
            SettingsTree.Press(scene.Window, Key.Escape, PhysicalKey.Escape);

            scene.Model.IsConfirming.ShouldBeFalse();
            scene.Model.IsOpen.ShouldBeTrue();
            closed.ShouldBe(0);
            scene.View.Details.IsVisible.ShouldBeFalse();
            scene.View.Status.Text.ShouldBe(CheckpointsCopy.Cancelled);
            SettingsTree.FocusedName(scene.Window).ShouldBe(CheckpointsCopy.RewindName(1));

            Click(scene, RowButton(scene, CheckpointsCopy.RewindName(2)));
            scene.Model.IsConfirming.ShouldBeTrue();
            Click(scene, scene.View.Cancel);

            scene.Model.IsConfirming.ShouldBeFalse();
            scene.View.Status.Text.ShouldBe(CheckpointsCopy.Cancelled);
            SettingsTree.FocusedName(scene.Window).ShouldBe(CheckpointsCopy.RewindName(2));
            scene.Backend.Rewinds.ShouldBeEmpty();

            SettingsTree.Press(scene.Window, Key.Escape, PhysicalKey.Escape);

            closed.ShouldBe(1);
            scene.Model.IsOpen.ShouldBeFalse();
            scene.View.IsEffectivelyVisible.ShouldBeFalse();
            scene.Window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The Close button closes the model and raises the close request once.</summary>
    [Fact]
    public async Task CloseClosesTheModelAndRaisesTheRequest()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var scene = Open(1);
            var closed = 0;
            scene.View.CloseRequested += (_, _) => closed++;

            Click(scene, scene.View.Close);

            closed.ShouldBe(1);
            scene.Model.IsOpen.ShouldBeFalse();
            scene.View.IsEffectivelyVisible.ShouldBeFalse();
            scene.Window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Preview shows the files a rewind would change without a confirmation or a write; Refresh clears it.</summary>
    [Fact]
    public async Task PreviewListsTheFilesWithoutAskingOrWriting()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var scene = Open(2, Restore("a.txt"), Restore("b.txt"));
            scene.View.Details.IsVisible.ShouldBeFalse();

            Click(scene, RowButton(scene, CheckpointsCopy.PreviewName(1)));

            scene.Model.IsConfirming.ShouldBeFalse();
            scene.Backend.Rewinds.ShouldBeEmpty();
            scene.View.Details.IsVisible.ShouldBeTrue();
            scene.View.Confirmation.IsVisible.ShouldBeFalse();
            scene.View.Confirm.IsEffectivelyVisible.ShouldBeFalse();
            scene.View.PreviewFiles.ItemCount.ShouldBe(2);
            SettingsTree.Texts(scene.View).ShouldContain("a.txt: a.txt detail", StringComparer.Ordinal);
            scene.View.Status.Text.ShouldBe(CheckpointsCopy.PreviewSummary(1, scene.Model.Preview!));

            Click(scene, scene.View.Refresh);
            scene.View.Details.IsVisible.ShouldBeFalse();
            scene.Window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Undo last turn asks to rewind the newest checkpoint and waits for the confirmation.</summary>
    [Fact]
    public async Task UndoLastTurnAsksForTheNewestCheckpoint()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var scene = Open(3);

            Click(scene, scene.View.Undo);

            scene.Model.IsConfirming.ShouldBeTrue();
            scene.Model.PendingTurnId.ShouldBe(3);
            scene.View.Details.IsVisible.ShouldBeTrue();
            scene.Backend.Rewinds.ShouldBeEmpty();
            SettingsTree.FocusedName(scene.Window).ShouldBe(CheckpointsViewText.CancelName);
            scene.Window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The status line is a named polite live region, assertive while it reports a failure and shown in the error tone, polite again after a success.</summary>
    [Fact]
    public async Task TheStatusLineIsAssertiveOnlyWhileItReportsAFailure()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var scene = Open(1);
            AutomationProperties.GetLiveSetting(scene.View.Status).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(scene.View.Status).ShouldBe(CheckpointsViewText.StatusName);

            scene.Backend.Failure = new InvalidOperationException("backend down");
            Click(scene, scene.View.Refresh);

            scene.Model.HasError.ShouldBeTrue();
            scene.View.Status.Text.ShouldBe(CheckpointsCopy.Failed("backend down"));
            AutomationProperties.GetLiveSetting(scene.View.Status).ShouldBe(AutomationLiveSetting.Assertive);
            scene.View.Status.Classes.Contains("tone-error").ShouldBeTrue();

            scene.Backend.Failure = null;
            Click(scene, scene.View.Refresh);

            scene.Model.HasError.ShouldBeFalse();
            AutomationProperties.GetLiveSetting(scene.View.Status).ShouldBe(AutomationLiveSetting.Polite);
            scene.View.Status.Classes.Contains("tone-error").ShouldBeFalse();
            scene.Window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every keyboard stop has an accessible name, every visible button is at least the minimum target, and Tab visits the toolbar, the confirmation and then the rows, in that order.</summary>
    [Fact]
    public async Task EveryControlIsNamedBigEnoughAndInKeyboardOrder()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var scene = Open(1);

            SettingsTree.Unnamed(scene.View).ShouldBeEmpty();
            SettingsTree.TabStops(scene.View).Select(static stop => AutomationProperties.GetName(stop)).ToList().ShouldBe(
            [
                CheckpointsViewText.RefreshName,
                CheckpointsViewText.UndoLastName,
                CheckpointsViewText.CloseName,
                CheckpointsCopy.PreviewName(1),
                CheckpointsCopy.RewindName(1),
            ]);

            Click(scene, RowButton(scene, CheckpointsCopy.RewindName(1)));

            SettingsTree.Unnamed(scene.View).ShouldBeEmpty();
            SettingsTree.TabStops(scene.View).Select(static stop => AutomationProperties.GetName(stop)).ToList().ShouldBe(
            [
                CheckpointsViewText.RefreshName,
                CheckpointsViewText.UndoLastName,
                CheckpointsViewText.CloseName,
                CheckpointsViewText.ConfirmName,
                CheckpointsViewText.CancelName,
                CheckpointsCopy.PreviewName(1),
                CheckpointsCopy.RewindName(1),
            ]);

            var buttons = SettingsTree.Visible<Button>(scene.View).Where(static button => button.TemplatedParent is null).ToList();
            buttons.Count.ShouldBe(7);
            foreach (var button in buttons)
            {
                button.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
                button.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            }

            scene.Window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }
}
