#pragma warning disable CA2007
#pragma warning disable MA0004
#pragma warning disable xUnit1051
using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using Autonogrammer.Harness.BrowserUse.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PuppeteerSharp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Policy;

/// <summary>Document hops abort before a denied target is hit. Stage-2 cards are host-observed.</summary>
public sealed class NavigationGateTests
{
    [Fact]
    public async Task RedirectToUngrantedPort_Metadata_AndRfc1918_ProducesZeroHitsInTheTargetLog()
    {
        await using var site = LocalTestSite.Start();
        var grants = new OriginGrants();
        grants.TryAdd(new LocalOriginGrant("127.0.0.1", site.GrantedPort)).ShouldBeTrue();
        var requested = new Uri("http://127.0.0.1:" + site.GrantedPort + "/");
        var session = new ScriptedCdpSession();
        var gate = new NavigationGate(new BrowserUseOptions(), grants, requested);
        await gate.AttachAsync(session).ConfigureAwait(true);
        session.Commands[0].Method.ShouldBe("Fetch.enable");
        var enable = session.Commands[0].Args!;
        var patterns = (Array)enable.GetType().GetProperty("patterns")!.GetValue(enable)!;
        var first = patterns.GetValue(0)!;
        first.GetType().GetProperty("resourceType")!.GetValue(first).ShouldBe("Document");

        await session.PauseAsync("1", "Document", site.RedirectUrl("http://127.0.0.1:" + site.UngrantedPort + "/hit").AbsoluteUri).ConfigureAwait(true);
        session.Decisions.Any(decision => string.Equals(decision, "Fetch.continueRequest", StringComparison.Ordinal)).ShouldBeTrue();
        await session.PauseAsync("2", "Document", "http://127.0.0.1:" + site.UngrantedPort + "/hit").ConfigureAwait(true);
        await session.PauseAsync("3", "Document", "http://169.254.169.254/latest/meta-data").ConfigureAwait(true);
        await session.PauseAsync("4", "Document", "http://10.1.2.3/secret").ConfigureAwait(true);

        site.UngrantedLog.Count.ShouldBe(0);
        session.TargetLog.ShouldNotContain(url => url.Contains("169.254.169.254", StringComparison.Ordinal));
        session.TargetLog.ShouldNotContain(url => url.Contains("10.1.2.3", StringComparison.Ordinal));
        session.TargetLog.ShouldNotContain(url => url.Contains(":" + site.UngrantedPort.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/", StringComparison.Ordinal));
        session.Decisions.Count(decision => string.Equals(decision, "Fetch.failRequest", StringComparison.Ordinal)).ShouldBe(3);
    }

    [Fact]
    public async Task NewTopLevelOrigin_RaisesStage2Card_WithUrlWithoutQuery_AndQueryLengthLine()
    {
        var query = new string('a', 70);
        var url = "https://example.com/path?" + query + "#frag";
        CoworkCallPreview? card = null;
        var gate = new NavigationGate(new BrowserUseOptions(), new OriginGrants(), new Uri("https://start.example/"), preview =>
        {
            card = preview;
            return Task.FromResult(true);
        });
        var session = new ScriptedCdpSession();
        await gate.AttachAsync(session).ConfigureAwait(true);
        await session.PauseAsync("9", "Document", url).ConfigureAwait(true);
        card.ShouldNotBeNull();
        card.Target.ShouldBe("https://example.com/path");
        card.Target!.ShouldNotContain("?");
        card.Details.Any(line => string.Equals(line, "this address carries 70 characters of data", StringComparison.Ordinal)).ShouldBeTrue();
        card.Headline.Contains(query, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public async Task SubresourceLoads_InPromptOnNewOrigin_DoNotRaiseCards_AcceptedResidual()
    {
        var raised = false;
        var gate = new NavigationGate(
            new BrowserUseOptions { PolicyMode = PolicyMode.PromptOnNewOrigin },
            new OriginGrants(),
            new Uri("https://start.example/"),
            _ =>
            {
                raised = true;
                return Task.FromResult(false);
            });
        var session = new ScriptedCdpSession();
        await gate.AttachAsync(session).ConfigureAwait(true);
        await session.PauseAsync("script", "Script", "https://cdn.example/app.js").ConfigureAwait(true);
        raised.ShouldBeFalse();
        session.Decisions.Any(decision => string.Equals(decision, "Fetch.continueRequest", StringComparison.Ordinal)).ShouldBeTrue();
    }

    private sealed class ScriptedCdpSession : ICDPSession
    {
        private readonly List<(string Method, object? Args)> _commands = [];
        private readonly List<string> _decisions = [];
        private readonly List<string> _targetLog = [];
        private readonly Dictionary<string, string> _urls = [];

        public List<(string Method, object? Args)> Commands => _commands;

        public List<string> Decisions => _decisions;

        public List<string> TargetLog => _targetLog;

        public ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;

        public string Id => "scripted";

        public string CloseReason => string.Empty;

        public bool Detached => false;

        public event EventHandler? Disconnected;

        public event EventHandler<SessionEventArgs>? SessionAttached;

        public event EventHandler<SessionEventArgs>? SessionDetached;

        public event EventHandler<MessageEventArgs>? MessageReceived;

        public async Task PauseAsync(string requestId, string resourceType, string url)
        {
            _urls[requestId] = url;
            var escaped = url.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
            var json = "{\"requestId\":\"" + requestId + "\",\"resourceType\":\"" + resourceType + "\",\"request\":{\"url\":\"" + escaped + "\"}}";
            using var document = JsonDocument.Parse(json);
            var args = new MessageEventArgs();
            typeof(MessageEventArgs).GetProperty(nameof(MessageEventArgs.MessageID))!.SetValue(args, "Fetch.requestPaused");
            typeof(MessageEventArgs).GetProperty(nameof(MessageEventArgs.MessageData))!.SetValue(args, document.RootElement.Clone());
            var before = _decisions.Count;
            MessageReceived?.Invoke(this, args);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (_decisions.Count == before && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10).ConfigureAwait(true);
            }
        }

        public Task DetachAsync() => Task.CompletedTask;

        public void Close(string closeReason) => _ = closeReason;

        public Task<T> SendAsync<T>(string method, object? args = null, CommandOptions? options = null)
        {
            _ = options;
            _ = RecordAsync(method, args);
            return Task.FromResult<T>(default!);
        }

        public async Task<JsonElement?> SendAsync(string method, object? args = null, bool waitForCallback = true, CommandOptions? options = null)
        {
            _ = waitForCallback;
            _ = options;
            await RecordAsync(method, args).ConfigureAwait(false);
            return null;
        }

        private async Task RecordAsync(string method, object? args)
        {
            _commands.Add((method, args));
            if (string.Equals(method, "Fetch.continueRequest", StringComparison.Ordinal) || string.Equals(method, "Fetch.failRequest", StringComparison.Ordinal))
            {
                _decisions.Add(method);
            }

            if (!string.Equals(method, "Fetch.continueRequest", StringComparison.Ordinal) || args is null)
            {
                return;
            }

            _ = args;
            var id = args.GetType().GetProperty("requestId")?.GetValue(args) as string ?? string.Empty;
            if (!_urls.TryGetValue(id, out var url))
            {
                return;
            }

            _targetLog.Add(url);
            if (url.StartsWith("http://127.0.0.1:", StringComparison.Ordinal))
            {
                // A browser pauses every redirect hop, so the scripted fetch must not follow one without the gate's decision.
                using var handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
                using var client = new HttpClient(handler);
                _ = await client.GetAsync(new Uri(url)).ConfigureAwait(false);
            }

            Disconnected?.Invoke(this, EventArgs.Empty);
            _ = SessionAttached;
            _ = SessionDetached;
        }
    }
}
