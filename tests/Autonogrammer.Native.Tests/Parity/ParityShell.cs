using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.State;

namespace Autonogrammer.Native.Tests.Parity;

/// <summary>
/// A shell under test over one selected session: the fixture that owns the panel and the runtime, and the window once it is shown. It
/// replaces the single main view the shell was before the Agent Panel.
/// </summary>
internal sealed class ParityShell : IAsyncDisposable
{
    /// <summary>Creates the shell over the send path of its one selected session.</summary>
    /// <param name="prompts">The send path, or null when no engine is composed.</param>
    public ParityShell(IPromptChannel? prompts)
    {
        Fixture = ShellFixture.CreateWithSession(prompts is null ? null : new SessionScript { Prompts = prompts });
    }

    /// <summary>Creates the shell over a send path that is built when the shell is.</summary>
    /// <param name="prompts">Builds the send path of the session.</param>
    public ParityShell(Func<IPromptChannel> prompts)
    {
        ArgumentNullException.ThrowIfNull(prompts);

        Fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = prompts() });
    }

    /// <summary>Gets the fixture the shell is made of.</summary>
    public ShellFixture Fixture { get; }

    /// <summary>Gets or sets the shown window, or null before <see cref="ParityShellHarness.Show"/>.</summary>
    public MainWindow? Window { get; set; }

    /// <summary>Gets the shown window, or fails the test when the shell was not shown.</summary>
    public MainWindow ShownWindow => Window ?? throw new InvalidOperationException("The shell was not shown.");

    /// <summary>Gets the main view of the shown window.</summary>
    public MainView Main => ShellHarness.MainViewOf(ShownWindow);

    /// <summary>Gets the runtime of the selected session.</summary>
    public AgentSessionRuntime Runtime => Fixture.Runtime(ShellHarness.FirstSessionId);

    /// <summary>Gets the transcript view model of the selected session.</summary>
    public SessionViewModel Session => Runtime.Session;

    /// <summary>Gets the composer view model of the selected session.</summary>
    public ComposerViewModel Composer => Runtime.Composer;

    /// <summary>Closes the window and disposes every runtime, which disposes each session's send path once.</summary>
    /// <returns>A task that completes when everything stopped.</returns>
    public ValueTask DisposeAsync() => Fixture.DisposeAsync();
}
