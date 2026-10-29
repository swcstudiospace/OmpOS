using System.Diagnostics;
using Autonogrammer.Harness.Mcp;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Views.Mcp;
using Autonogrammer.Presentation;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// A close fault in the main view reaches the stderr trace listener that App startup installs, so the failure is visible to the operator
/// of the desktop app. The close path itself is covered by <c>MainViewCloseFailureTests</c> (M-18, D8).
/// </summary>
public sealed class MainViewCloseFaultStderrTests
{
    /// <summary>How long the test waits for the close fault to reach the standard-error writer.</summary>
    private static readonly TimeSpan TraceTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The fragment the MCP host's close continuation traces when its dispose fails.</summary>
    private const string McpMarker = "could not close the MCP tools";

    /// <summary>Closing the window with a failing MCP host writes the failure to the stderr listener, with the dispose error's message.</summary>
    [Fact]
    public async Task TheStderrListenerInstalledAtStartupReceivesACloseFault()
    {
        await using var session = TestAppBuilder.StartSession();
        var written = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var error = new MarkerWriter(written, McpMarker);
        App.StderrTraceListener? listener = null;
        try
        {
            await session.RunAsync(async () =>
            {
                // Installed on the UI thread after the session has started: the app's startup listener would otherwise replace it.
                listener = App.InstallStderrTraceListener(error);
                var fixture = ShellFixture.CreateWithSession();
                await using var fixtureScope = fixture.ConfigureAwait(true);
                var main = new MainView(fixture.Panel, Environment.CurrentDirectory, MotionPreference.Full, mcpHost: new DisposeFailingMcpHost());
                var window = new Window { Content = main, Width = 900, Height = 600 };
                window.Show();
                ShellHarness.Settle(window);
                main.Close();
                window.Close();
            }, TestContext.Current.CancellationToken).ConfigureAwait(true);

            var text = await written.Task.WaitAsync(TraceTimeout, TestContext.Current.CancellationToken).ConfigureAwait(true);
            text.ShouldContain("mcp dispose failed");
        }
        finally
        {
            if (listener is not null)
            {
                Trace.Listeners.Remove(listener);
                listener.Dispose();
            }
        }
    }

    /// <summary>An MCP host with no tools whose disposal fails the way a registry whose teardown breaks does.</summary>
    private sealed class DisposeFailingMcpHost : IMcpRegistryHost
    {
        /// <inheritdoc />
        public IReadOnlyList<McpToolDescriptor> Tools => [];

        /// <inheritdoc />
        public IReadOnlyList<McpToolDescriptor> GetOfferedTools(string sessionId) => [];

        /// <inheritdoc />
        public bool IsEnabled(string sessionId, McpToolId tool) => false;

        /// <inheritdoc />
        public void Enable(string sessionId, McpToolId tool)
        {
        }

        /// <inheritdoc />
        public void Disable(string sessionId, McpToolId tool)
        {
        }

        /// <summary>Fails the dispose the way a registry whose teardown breaks does.</summary>
        /// <returns>A value task that has faulted with the dispose failure.</returns>
        public ValueTask DisposeAsync() => ValueTask.FromException(new InvalidOperationException("mcp dispose failed"));
    }

    /// <summary>A text writer that completes once a line containing the marker is written, with that line.</summary>
    /// <param name="written">Completed with the first written line that reports the failed close.</param>
    /// <param name="marker">The fragment the close continuation writes on that failed close.</param>
    private sealed class MarkerWriter(TaskCompletionSource<string> written, string marker) : StringWriter
    {
        /// <inheritdoc />
        public override void Write(string? value)
        {
            base.Write(value);
            Record(value);
        }

        /// <inheritdoc />
        public override void WriteLine(string? value)
        {
            base.WriteLine(value);
            Record(value);
        }

        /// <summary>Completes the write task when the text reports a failed close.</summary>
        /// <param name="text">The text the writer received.</param>
        private void Record(string? text)
        {
            if (text is not null && text.Contains(marker, StringComparison.Ordinal))
            {
                written.TrySetResult(text);
            }
        }
    }
}
