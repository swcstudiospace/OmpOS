using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Harness.Automations.Tests;
using OmpOS.Native.Composition.Automations;
using OmpOS.Native.Tests.AgentLoop;
using OmpOS.Native.Tests.Workspace;
using OmpOS.Native.Views.Automations;
using OmpOS.Presentation.Automations;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Composition.Automations;

/// <summary>
/// The Automations page over the real desktop manager (a file in a temporary folder) on the headless desktop: the page says where local automations run, the editor offers the schedule only and
/// says why, a saved automation appears in the list and in the file, and every control is named.
/// </summary>
public sealed class AutomationsPageViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task WaitUntilAsync(INotifyPropertyChanged source, Func<bool> condition)
    {
        if (condition())
        {
            return;
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, PropertyChangedEventArgs e)
        {
            if (condition())
            {
                done.TrySetResult();
            }
        }

        source.PropertyChanged += Handler;
        try
        {
            await done.Task.WaitAsync(TimeSpan.FromSeconds(20), Token).ConfigureAwait(true);
        }
        finally
        {
            source.PropertyChanged -= Handler;
        }
    }

    private static List<string> Shown(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text)).Select(text => text.Text!)];

    private static AutomationsHost Host(TempTree tree, bool localAvailable = true) =>
        new(
            Path.Combine(tree.Root, "automations.json"),
            Path.Combine(tree.Root, "consent.json"),
            cloud: null,
            () => tree.Workspace,
            localAvailable,
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero)));

    /// <summary>The page opens over the desktop manager: the empty state, the sentence about where local automations run, and every control named.</summary>
    [Fact]
    public async Task ThePageOpensEmptyAndSaysWhereLocalAutomationsRun()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var tree = new TempTree();
            var host = Host(tree);
            await using var hostScope = host.ConfigureAwait(true);
            var model = new HostedAutomationsViewModel(host.Manager);
            var view = new AutomationsPageView { DataContext = model };
            var window = ViewHost.Show(view);
            await WaitUntilAsync(model, () => model.State == HostedAutomationsViewState.Empty).ConfigureAwait(true);
            ShellHarness.Settle(window);

            view.Empty.IsEffectivelyVisible.ShouldBeTrue();
            Shown(view).ShouldContain(AutomationsCopy.DesktopNote, StringComparer.Ordinal);
            ViewHost.UnnamedTabStops(view).ShouldBeEmpty();
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Without omp the page says local automations cannot run.</summary>
    [Fact]
    public async Task WithoutOmpThePageSaysLocalAutomationsCannotRun()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var tree = new TempTree();
            var host = Host(tree, localAvailable: false);
            await using var hostScope = host.ConfigureAwait(true);
            var model = new HostedAutomationsViewModel(host.Manager);
            var view = new AutomationsPageView { DataContext = model };
            var window = ViewHost.Show(view);
            await WaitUntilAsync(model, () => model.State == HostedAutomationsViewState.Empty).ConfigureAwait(true);
            ShellHarness.Settle(window);

            Shown(view).ShouldContain(AutomationsCopy.NoOmpNote, StringComparer.Ordinal);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The editor of a desktop automation has no webhook trigger and says why; a name, a prompt and a schedule save it, the list shows it and the file holds it.</summary>
    [Fact]
    public async Task TheEditorOffersTheScheduleOnlyAndSavesAnAutomation()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var tree = new TempTree();
            var host = Host(tree);
            await using var hostScope = host.ConfigureAwait(true);
            var model = new HostedAutomationsViewModel(host.Manager);
            var view = new AutomationsPageView { DataContext = model };
            var window = ViewHost.Show(view);
            await WaitUntilAsync(model, () => model.State == HostedAutomationsViewState.Empty).ConfigureAwait(true);

            view.EmptyNew.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ShellHarness.Settle(window);
            var editor = view.EditorForm.ShouldNotBeNull();

            editor.WebhookEnabledField.IsEffectivelyVisible.ShouldBeFalse("a desktop automation starts from its schedule");
            Shown(editor).ShouldContain(AutomationsCopy.ScheduleOnlyNote, StringComparer.Ordinal);
            editor.Target.Items.Count.ShouldBe(1);
            ViewHost.UnnamedTabStops(view).ShouldBeEmpty();

            editor.NameField.Text = "Morning summary";
            editor.PromptField.Text = AutomationFixtures.Prompt;
            editor.CronField.Text = "30 9 * * *";
            ShellHarness.Settle(window);
            editor.Save.IsEnabled.ShouldBeTrue();
            editor.Save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(model, () => model.Rows.Count == 1).ConfigureAwait(true);
            ShellHarness.Settle(window);

            var stored = (await host.Store.ListAsync(Token).ConfigureAwait(true)).ShouldHaveSingleItem();
            stored.Name.ShouldBe("Morning summary");
            stored.Triggers.ShouldHaveSingleItem().Cron.ShouldBe("30 9 * * *");
            window.Close();
        }, Token).ConfigureAwait(true);
    }
}
