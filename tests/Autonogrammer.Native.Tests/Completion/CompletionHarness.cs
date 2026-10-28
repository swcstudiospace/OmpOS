using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Autonogrammer.Harness.Completion;
using Autonogrammer.Native.Views.Completion;
using Xunit;

namespace Autonogrammer.Native.Tests.Completion;

/// <summary>Shared helpers for the inline completion tests: builds the controls, shows them, settles layout and sends keys.</summary>
internal static class CompletionHarness
{
    /// <summary>The debounce the tests use, so a request follows an edit quickly without racing it.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(20);

    private static readonly Uri Endpoint = new("http://completion.test/v1/complete");

    /// <summary>Gets the cancellation token of the running test.</summary>
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Creates a client for a configured provider that answers through the counting stub.</summary>
    public static CompletionClient Configured(CountingProvider provider) =>
        new(CompletionSettings.ForEndpoint(Endpoint), provider.Http);

    /// <summary>Creates a client with no provider configured. It must never send a request.</summary>
    public static CompletionClient Unconfigured(CountingProvider provider) =>
        new(CompletionSettings.None, provider.Http);

    /// <summary>Creates a completion box with a short debounce.</summary>
    /// <param name="client">The client the box asks for suggestions.</param>
    public static InlineCompletionBox NewBox(CompletionClient? client) =>
        new() { Client = client, DebounceInterval = Debounce };

    /// <summary>Shows the content in an active window, and gives it the layout passes a real window gets.</summary>
    /// <param name="content">The content to show.</param>
    /// <returns>The shown window.</returns>
    public static CompletionWindow Show(Control content)
    {
        var window = new CompletionWindow { Width = 600, Height = 400, Content = content };
        window.Show();
        window.Activate();
        Settle(window);
        return window;
    }

    /// <summary>Runs pending dispatcher work and several layout passes.</summary>
    /// <param name="window">The window to settle.</param>
    public static void Settle(Window window)
    {
        for (var pass = 0; pass < 5; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.GetLayoutManager()?.ExecuteLayoutPass();
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Presses and releases one key on the window, as the keyboard would, and settles the layout.</summary>
    /// <param name="window">The shown window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    /// <param name="shift">Whether Shift is held.</param>
    public static void Press(Window window, Key key, PhysicalKey physical, bool shift = false)
    {
        var modifiers = shift ? (RawInputModifiers)KeyModifiers.Shift : default;
        window.KeyPress(key, modifiers, physical, null);
        Settle(window);
    }

    /// <summary>Lets background work run for a duration, settling the layout as it goes.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="duration">How long to run.</param>
    public static async Task PumpAsync(Window window, TimeSpan duration)
    {
        var until = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < until)
        {
            Settle(window);
            await Task.Delay(10, Token).ConfigureAwait(true);
        }

        Settle(window);
    }

    /// <summary>Waits, for at most ten seconds, until the condition holds.</summary>
    /// <param name="window">The shown window to settle while waiting.</param>
    /// <param name="condition">The condition to wait for.</param>
    /// <param name="because">What the wait is for, named in the timeout.</param>
    /// <exception cref="TimeoutException">The condition did not hold in time.</exception>
    public static async Task WaitForAsync(Window window, Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {because}.");
            }

            Settle(window);
            await Task.Delay(10, Token).ConfigureAwait(true);
        }

        Settle(window);
    }
}
