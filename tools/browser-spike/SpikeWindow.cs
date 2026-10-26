using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;

namespace OmpOS.BrowserSpike;

/// <summary>Hosts the native web view, loads the local fixture and reads the button's selector and box back.</summary>
internal sealed class SpikeWindow : Window
{
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

    // Returns a JSON object as a string. The selector must resolve back to the same element.
    private const string ProbeScript =
        "(function () {" +
        "var el = document.getElementById('go');" +
        "if (!el) { return JSON.stringify({ found: false }); }" +
        "var r = el.getBoundingClientRect();" +
        "var sel = '#' + el.id;" +
        "return JSON.stringify({ found: true, selector: sel, resolves: document.querySelector(sel) === el," +
        " x: r.x, y: r.y, width: r.width, height: r.height });" +
        "})()";

    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly NativeWebView _web;
    private readonly DispatcherTimer _deadlineTimer;
    private bool _finished;

    /// <summary>The in-flight probe. Held so a fault is reported by its continuation rather than lost.</summary>
    private Task? Probe { get; set; }

    public SpikeWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _desktop = desktop;
        Title = "OmpOS browser spike (ADR-0009)";
        Width = 800;
        Height = 600;

        _web = new NativeWebView();
        string? forced = Environment.GetEnvironmentVariable(Program.AdapterOverrideVariable);
        if (!string.IsNullOrEmpty(forced))
        {
            if (!Enum.TryParse(forced, ignoreCase: false, out WebViewAdapterType adapter))
            {
                throw new InvalidOperationException($"{Program.AdapterOverrideVariable}={forced} is not a WebViewAdapterType name");
            }

            _web.AdapterPreference = new List<WebViewAdapterType> { adapter };
        }

        _web.NavigationCompleted += OnNavigationCompleted;
        Content = _web;

        _deadlineTimer = new DispatcherTimer { Interval = Deadline };
        _deadlineTimer.Tick += (_, _) => Finish(SpikeVerdict.Failed("deadline", $"no result within {Deadline.TotalSeconds} s; adapter={_web.AdapterInfo?.ToString() ?? "null"}"));

        Opened += (_, _) =>
        {
            _deadlineTimer.Start();
            _web.Source = FixtureUri();
        };
    }

    private static Uri FixtureUri() =>
        new(Path.Combine(AppContext.BaseDirectory, "fixtures", "index.html"), UriKind.Absolute);

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (_finished)
        {
            return;
        }

        if (!e.IsSuccess)
        {
            Finish(SpikeVerdict.Failed("load", $"navigation to {FixtureUri()} reported IsSuccess=false"));
            return;
        }

        Probe = RunProbeAsync().ContinueWith(
            t => Finish(SpikeVerdict.Failed("probe", DescribeFault(t.Exception))),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async Task RunProbeAsync()
    {
        string adapter = _web.AdapterInfo?.ToString() ?? "null";
        string? raw = await _web.InvokeScript(ProbeScript).ConfigureAwait(true);
        Finish(raw is null
            ? SpikeVerdict.Failed("probe", $"InvokeScript returned null; adapter={adapter}")
            : SpikeVerdict.FromProbe(raw, adapter));
    }

    private static string DescribeFault(AggregateException? exception)
    {
        Exception? inner = exception?.GetBaseException();
        return inner is null ? "probe faulted without an exception" : $"{inner.GetType().FullName}: {inner.Message}";
    }

    private void Finish(SpikeVerdict verdict)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _deadlineTimer.Stop();
        Console.WriteLine(verdict.ToLine());
        _desktop.Shutdown(verdict.Passed ? 0 : 1);
    }
}
