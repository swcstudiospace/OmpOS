using Avalonia.Controls;
using OmpOS.Native.Tests.AgentPanel.Views;
using OmpOS.Native.Tests.Settings;
using OmpOS.Native.Views.Cloud;
using OmpOS.Presentation.Cloud;

namespace OmpOS.Native.Tests.Cloud;

/// <summary>A shown Cloud agents page in a headless window over a <see cref="FakeCloudAgentsHost"/>, with helpers that drive it the way a keyboard does.</summary>
internal sealed class CloudScreen : IDisposable
{
    private CloudScreen(FakeCloudAgentsHost host, ManualTimeProvider clock, CloudAgentsViewModel model, CloudAgentsPageView view, Window window)
    {
        Host = host;
        Clock = clock;
        Model = model;
        View = view;
        Window = window;
    }

    /// <summary>Gets the host the page talks to.</summary>
    internal FakeCloudAgentsHost Host { get; }

    /// <summary>Gets the clock of the host and the model.</summary>
    internal ManualTimeProvider Clock { get; }

    /// <summary>Gets the model the page draws.</summary>
    internal CloudAgentsViewModel Model { get; }

    /// <summary>Gets the page.</summary>
    internal CloudAgentsPageView View { get; }

    /// <summary>Gets the window that shows the page.</summary>
    internal Window Window { get; }

    /// <summary>
    /// Runs a test body on the headless UI thread with a page over a host the caller prepares. The page starts loading when it is shown, so
    /// <paramref name="arrange"/> runs first.
    /// </summary>
    /// <param name="arrange">Prepares the host before the page is shown, or null.</param>
    /// <param name="body">The test body.</param>
    /// <param name="cancellationToken">The test token.</param>
    /// <returns>A task that completes when the body and the clean-up ended.</returns>
    internal static async Task RunAsync(Action<FakeCloudAgentsHost>? arrange, Func<CloudScreen, Task> body, CancellationToken cancellationToken)
    {
        var session = TestAppBuilder.StartSession();
        await using var sessionScope = session.ConfigureAwait(true);
        await session.RunAsync(async () =>
        {
            var clock = new ManualTimeProvider();
            var host = new FakeCloudAgentsHost(clock);
            arrange?.Invoke(host);
            using var screen = Show(host, clock);
            await body(screen).ConfigureAwait(true);
        }, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Shows the page over a host and lets the first layout pass run.</summary>
    /// <param name="host">The host.</param>
    /// <param name="clock">The clock.</param>
    /// <returns>The shown screen.</returns>
    internal static CloudScreen Show(FakeCloudAgentsHost host, ManualTimeProvider clock)
    {
        var model = new CloudAgentsViewModel(host, clock);
        var view = new CloudAgentsPageView { DataContext = model };
        var window = new Window { Width = 1000, Height = 1800, Content = view };
        window.Show();
        var screen = new CloudScreen(host, clock, model, view, window);
        screen.Settle();
        return screen;
    }

    /// <summary>Runs pending dispatcher work and one layout pass.</summary>
    internal void Settle() => SettingsTree.SettleSafe(Window);

    /// <summary>Focuses a button and presses Enter on it, then settles.</summary>
    /// <param name="button">The button.</param>
    internal void Click(Button button)
    {
        SettingsTree.Click(Window, button);
        Settle();
    }

    /// <summary>Gets the visible button with an accessible name.</summary>
    /// <param name="name">The accessible name.</param>
    /// <returns>The button.</returns>
    internal Button ButtonNamed(string name) => SettingsTree.Named<Button>(View, name);

    /// <summary>Gets the accessible name of the focused control, or null.</summary>
    /// <returns>The name.</returns>
    internal string? FocusedName() => SettingsTree.FocusedName(Window);

    /// <summary>Selects the row of an agent the way a click does.</summary>
    /// <param name="agentId">The agent.</param>
    internal void SelectRow(string agentId)
    {
        View.Agents.SelectedItem = Model.Rows.Single(row => string.Equals(row.Id, agentId, StringComparison.Ordinal));
        Settle();
    }

    /// <summary>Gets the accessible names of every visible control, text and help.</summary>
    /// <returns>The strings.</returns>
    internal IReadOnlyList<string> Everything() => SettingsTree.Everything(View);

    /// <inheritdoc/>
    public void Dispose()
    {
        Window.Close();
        Model.Dispose();
    }
}
