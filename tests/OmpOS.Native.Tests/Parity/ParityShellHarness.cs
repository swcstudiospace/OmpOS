using Avalonia.Controls;
using OmpOS.Contracts.Ui;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.RunControls;
using OmpOS.Native.Views;
using OmpOS.Parity.Support;
using OmpOS.Presentation;
using OmpOS.Presentation.State;

namespace OmpOS.Native.Tests.Parity;

/// <summary>
/// Shows the native shell over a chosen send path and drives it the way the user does: typing a draft, pressing send, and activating a
/// transcript control. The parity checks use it where a turn must really run through the shell.
/// </summary>
internal static class ParityShellHarness
{
    /// <summary>How many layout passes settle a window, so a scroll that follows a layout change is applied.</summary>
    private const int SettlePasses = 3;

    /// <summary>Creates the shell over the send path. Without one, the shell refuses every send, as an unconnected shell does.</summary>
    /// <param name="prompts">The send path, or null when no engine is composed.</param>
    /// <returns>The main view, not yet shown.</returns>
    public static MainView CreateMain(IPromptChannel? prompts)
    {
        var workspace = Environment.CurrentDirectory;
        return prompts is null
            ? new MainView(new UnconnectedToolApprovalBridge(), new UnconnectedRunInput(), workspace, prompts: null, MotionPreference.Full)
            : new MainView(new UnconnectedToolApprovalBridge(), new EngineRunInput(), workspace, prompts, MotionPreference.Full);
    }

    /// <summary>Shows the main view in a window tall enough to leave the transcript room to scroll, and settles it.</summary>
    /// <param name="main">The main view to show.</param>
    /// <returns>The shown window.</returns>
    public static Window Show(MainView main)
    {
        var window = new Window { Content = main, Width = 900, Height = 600 };
        window.Show();
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
    /// <param name="main">The main view that holds the composer.</param>
    /// <param name="text">The draft to send.</param>
    public static void Send(MainView main, string text)
    {
        var composer = ComposerOf(main);
        composer.FindControl<TextBox>("PromptInput")!.Text = text;
        RunControlsHarness.Click(composer.FindControl<Button>("SendButton")!);
    }

    /// <summary>Gets the transcript of the main view.</summary>
    /// <param name="main">The main view.</param>
    /// <returns>The session view the transcript host shows.</returns>
    public static SessionView Transcript(MainView main) => (SessionView)main.FindControl<ContentControl>("TranscriptHost")!.Content!;

    /// <summary>Gets the scroll region of the transcript of the main view.</summary>
    /// <param name="main">The main view.</param>
    /// <returns>The transcript's scroll viewer.</returns>
    public static ScrollViewer Scroll(MainView main) => Transcript(main).FindControl<ScrollViewer>("TranscriptScroll")!;

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

    private static ComposerView ComposerOf(MainView main) => (ComposerView)main.FindControl<ContentControl>("ComposerHost")!.Content!;
}
