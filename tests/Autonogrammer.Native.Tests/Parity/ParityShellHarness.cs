using Autonogrammer.Contracts.Ui;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.RunControls;
using Autonogrammer.Native.Views;
using Autonogrammer.Parity.Support;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.State;
using Avalonia.Controls;

namespace Autonogrammer.Native.Tests.Parity;

/// <summary>
/// Shows the native shell over a chosen send path and drives it the way the user does: typing a draft, pressing send, and activating a
/// transcript control. The parity checks use it where a turn must really run through the shell.
/// </summary>
internal static class ParityShellHarness
{
    /// <summary>How many layout passes settle a window, so a scroll that follows a layout change is applied.</summary>
    private const int SettlePasses = 3;

    /// <summary>Creates the shell over the send path of its one selected session. Without one, the shell refuses every send, as an unconnected shell does.</summary>
    /// <param name="prompts">The send path, or null when no engine is composed.</param>
    /// <returns>The shell, not yet shown. Dispose it to close the window and dispose the session's send path.</returns>
    public static ParityShell CreateMain(IPromptChannel? prompts) => new(prompts);

    /// <summary>Shows the shell in a window tall enough to leave the transcript room to scroll, and settles it.</summary>
    /// <param name="shell">The shell to show.</param>
    /// <returns>The shown window.</returns>
    public static MainWindow Show(ParityShell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        var window = ShellHarness.Show(shell.Fixture, MotionPreference.Full);
        shell.Window = window;
        Settle(window);
        return window;
    }

    /// <summary>Runs the layout passes a shown window needs before its scroll position and item containers are final.</summary>
    /// <param name="window">The window to settle.</param>
    public static void Settle(Window window)
    {
        for (var pass = 0; pass < SettlePasses; pass++)
        {
            ShellHarness.Settle(window);
        }
    }

    /// <summary>Types the text into the composer and presses its send control, as the user does.</summary>
    /// <param name="shell">The shown shell that holds the composer.</param>
    /// <param name="text">The draft to send.</param>
    public static void Send(ParityShell shell, string text)
    {
        var window = shell.ShownWindow;
        ShellHarness.Prompt(window).Text = text;
        RunControlsHarness.Click(ShellHarness.Send(window));
    }

    /// <summary>Gets the transcript of the shown shell.</summary>
    /// <param name="shell">The shown shell.</param>
    /// <returns>The session view the transcript host shows.</returns>
    public static SessionView Transcript(ParityShell shell) => ShellHarness.Transcript(shell.ShownWindow);

    /// <summary>Gets the scroll region of the transcript of the shown shell.</summary>
    /// <param name="shell">The shown shell.</param>
    /// <returns>The transcript's scroll viewer.</returns>
    public static ScrollViewer Scroll(ParityShell shell) => ShellHarness.Scroll(shell.ShownWindow);

    /// <summary>Folds user messages into the session, one stream event each, numbered from <paramref name="first"/> to <paramref name="last"/>.</summary>
    /// <param name="session">The session to fold the messages into.</param>
    /// <param name="first">The first message number.</param>
    /// <param name="last">The last message number, inclusive.</param>
    public static void AppendMessages(SessionViewModel session, int first, int last)
    {
        for (var number = first; number <= last; number++)
        {
            session.ApplyEvent(ParityEvents.Create(number, ParityEvents.Block(new UserMessageBlock { Id = $"u-{number}", Text = $"Message {number}" })));
        }
    }

    /// <summary>Whether a transcript shows its newest output: its offset is at the end of its extent, within a pixel.</summary>
    /// <param name="scroll">The transcript's scroll viewer.</param>
    /// <returns>True when the view is at the latest output.</returns>
    public static bool IsAtLatest(ScrollViewer scroll) =>
        scroll.Offset.Y >= Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height) - 1;
}
