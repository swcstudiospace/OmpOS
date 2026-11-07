using System.Text.Json;
using Autonogrammer.Contracts.Cowork.Browser;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.BrowserUse.Tools;
using Autonogrammer.Native.Cowork;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using PuppeteerSharp;
using Shouldly;

namespace Autonogrammer.Native.Tests.Shortcuts;

/// <summary>Raises Ctrl+Shift+. on a headless browser pane and observes Stop.</summary>
internal static class BrowserShortcutEffects
{
    /// <summary>The chord cancels the in-flight call token and revokes control.</summary>
    /// <param name="gesture">The catalogued gesture.</param>
    /// <returns>The task.</returns>
    internal static async Task StopAsync(KeyGesture gesture)
    {
        var root = Path.Combine(Path.GetTempPath(), "ag-browser-stop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var pages = new BlockingPages();
        using var composed = BrowserUseComposition.Wire(new BrowserUseCompositionRequest
        {
            StateDirectory = root,
            ForceAvailable = true,
            Pages = pages,
            Configure = static options => options.Enabled = true,
        });
        var window = new Window { Content = composed.Panel.View, Width = 480, Height = 320 };
        try
        {
            window.Show();
            composed.Panel.View.Focus();
            ShellHarness.Settle(window);
            var lease = composed.Host.BeginSession(new CoworkSessionContext
            {
                SessionId = "stop",
                WorkingDirectory = root,
                TrustedFolders = [root],
                ModelReadsImages = true,
            });
            await using var leaseLifetime = lease.ConfigureAwait(true);
            using var document = JsonDocument.Parse("{}");
            var invoke = composed.Host.InvokeAsync("stop", "browser_scroll", document.RootElement, CancellationToken.None);
            await pages.Opened.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            window.KeyPress(gesture.Key, (RawInputModifiers)gesture.KeyModifiers, PhysicalKey.Period, null);
            ShellHarness.Settle(window);
            var result = await invoke.ConfigureAwait(true);
            result.IsError.ShouldBeTrue();
            pages.Cancelled.ShouldBeTrue();
            composed.Control.Control.ShouldBe(BrowserControlState.Revoked);
            composed.Control.Gate.ShouldBeSameAs(composed.Gate);
        }
        finally
        {
            window.Close();
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class BlockingPages : IBrowserPageSource
    {
        public int Opens { get; private set; }

        public bool Cancelled { get; private set; }

        public TaskCompletionSource Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<ICDPSession> OpenAsync(string sessionId, CancellationToken cancellationToken)
        {
            Opens++;
            Opened.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Cancelled = true;
                throw;
            }

            throw new InvalidOperationException("The scripted page was not cancelled.");
        }
    }
}
