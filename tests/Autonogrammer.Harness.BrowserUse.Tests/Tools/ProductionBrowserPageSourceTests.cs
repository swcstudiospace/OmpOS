using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using Autonogrammer.Harness.BrowserUse.Tests.Support;
using Autonogrammer.Harness.BrowserUse.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PuppeteerSharp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Tools;

/// <summary>Each open owns one proxy and one browser; the proxy credential is answered Chromium-side on the page.</summary>
public sealed class ProductionBrowserPageSourceTests
{
    private const string ProxyPrefix = "--proxy-server=http://127.0.0.1:";
    private const string DeniedTarget = "http://127.0.0.1:9/";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EachOpen_CreatesOneProxyAndOneBrowserSession_AndEachProxyDiesWithItsBrowser()
    {
        var launcher = new FakeChromiumLauncher { PageFactory = static () => new ScriptedPage() };
        var host = new BrowserSessionHost(launcher, new BrowserUseOptions());
        var grantedFor = new List<string>();
        var source = NewSource(host, id =>
        {
            grantedFor.Add(id);
            return new OriginGrants();
        });

        _ = await source.OpenAsync("first", Token).ConfigureAwait(true);
        _ = await source.OpenAsync("second", Token).ConfigureAwait(true);

        launcher.Launches.ShouldBe(2);
        host.OpenCount.ShouldBe(2);
        source.Opens.ShouldBe(2);
        grantedFor.Count.ShouldBe(2);
        grantedFor[0].ShouldBe("first");
        grantedFor[1].ShouldBe("second");
        var first = ProxyPortOf(launcher.RequestAt(0));
        var second = ProxyPortOf(launcher.RequestAt(1));
        second.ShouldNotBe(first);
        (await AcceptsConnectionAsync(first).ConfigureAwait(true)).ShouldBeTrue();
        (await AcceptsConnectionAsync(second).ConfigureAwait(true)).ShouldBeTrue();

        await host.DisposeAsync().ConfigureAwait(true);

        host.OpenCount.ShouldBe(0);
        (await AcceptsConnectionAsync(first).ConfigureAwait(true)).ShouldBeFalse();
        (await AcceptsConnectionAsync(second).ConfigureAwait(true)).ShouldBeFalse();
    }

    [Fact]
    public async Task FailedLaunch_DisposesTheProxyItCreated()
    {
        var launcher = new RefusingLauncher();
        var host = new BrowserSessionHost(launcher, new BrowserUseOptions());
        try
        {
            var source = NewSource(host);

            await Should.ThrowAsync<InvalidOperationException>(() => source.OpenAsync("refused", Token).AsTask()).ConfigureAwait(true);

            launcher.Requests.Count.ShouldBe(1);
            (await AcceptsConnectionAsync(ProxyPortOf(launcher.Requests[0])).ConfigureAwait(true)).ShouldBeFalse();
            source.Opens.ShouldBe(0);
            host.OpenCount.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task FailedPageOpen_ClosesTheBrowserAndDisposesItsProxy()
    {
        var launcher = new FakeChromiumLauncher();
        var host = new BrowserSessionHost(launcher, new BrowserUseOptions());
        try
        {
            var source = NewSource(host);

            await Should.ThrowAsync<InvalidOperationException>(() => source.OpenAsync("no-page", Token).AsTask()).ConfigureAwait(true);

            var session = launcher.SessionAt(0);
            session.CdpOrder.ShouldBe(1);
            session.KillOrder.ShouldBe(2);
            (await AcceptsConnectionAsync(ProxyPortOf(launcher.RequestAt(0))).ConfigureAwait(true)).ShouldBeFalse();
            source.Opens.ShouldBe(0);
            host.OpenCount.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ProxyChallenge_IsAnsweredChromiumSide_WithTheSessionsProxyCredential()
    {
        var page = new ScriptedPage();
        var launcher = new FakeChromiumLauncher { PageFactory = () => page };
        var host = new BrowserSessionHost(launcher, new BrowserUseOptions());
        try
        {
            var source = NewSource(host);

            var opened = await source.OpenAsync("s", Token).ConfigureAwait(true);

            opened.ShouldBeSameAs(page);
            var enable = page.Commands.Single(static command => string.Equals(command.Method, "Fetch.enable", StringComparison.Ordinal));
            Params(enable.Args).GetProperty("handleAuthRequests").GetBoolean().ShouldBeTrue();

            page.Raise("Fetch.authRequired", Challenge("auth-1", "Proxy"));
            var answered = Params(await page.ArgsAsync("Fetch.continueWithAuth", 1).ConfigureAwait(true));
            answered.GetProperty("requestId").GetString().ShouldBe("auth-1");
            var response = answered.GetProperty("authChallengeResponse");
            response.GetProperty("response").GetString().ShouldBe("ProvideCredentials");
            var user = response.GetProperty("username").GetString() ?? string.Empty;
            var password = response.GetProperty("password").GetString() ?? string.Empty;

            // The proxy accepts the answered credential (it reaches the policy and refuses the target with 403), and
            // refuses any other value with 407, so the answer is the session's own credential and nothing else.
            var port = ProxyPortOf(launcher.RequestAt(0));
            (await StatusCodeAsync(port, DeniedTarget, user, password).ConfigureAwait(true)).ShouldBe(403);
            (await StatusCodeAsync(port, DeniedTarget, "wrong", "creds").ConfigureAwait(true)).ShouldBe(407);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task PausedRequest_IsContinued_AndNeverAnswered()
    {
        var page = new ScriptedPage();
        var launcher = new FakeChromiumLauncher { PageFactory = () => page };
        var host = new BrowserSessionHost(launcher, new BrowserUseOptions());
        try
        {
            _ = await NewSource(host).OpenAsync("s", Token).ConfigureAwait(true);

            page.Raise("Fetch.requestPaused", "{\"requestId\":\"req-1\",\"resourceType\":\"Document\",\"request\":{\"url\":\"http://127.0.0.1:9/\"}}");

            Params(await page.ArgsAsync("Fetch.continueRequest", 1).ConfigureAwait(true)).GetProperty("requestId").GetString().ShouldBe("req-1");
            page.Commands.Any(static command => string.Equals(command.Method, "Fetch.continueWithAuth", StringComparison.Ordinal)).ShouldBeFalse();
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task WebsiteChallenge_IsCancelled_AndARepeatedProxyChallengeIsCancelled()
    {
        var page = new ScriptedPage();
        var launcher = new FakeChromiumLauncher { PageFactory = () => page };
        var host = new BrowserSessionHost(launcher, new BrowserUseOptions());
        try
        {
            _ = await NewSource(host).OpenAsync("s", Token).ConfigureAwait(true);

            page.Raise("Fetch.authRequired", Challenge("site-1", "Server"));
            var site = Params(await page.ArgsAsync("Fetch.continueWithAuth", 1).ConfigureAwait(true));
            var siteResponse = site.GetProperty("authChallengeResponse");
            siteResponse.GetProperty("response").GetString().ShouldBe("CancelAuth");
            siteResponse.TryGetProperty("username", out _).ShouldBeFalse();
            siteResponse.TryGetProperty("password", out _).ShouldBeFalse();

            page.Raise("Fetch.authRequired", Challenge("proxy-1", "Proxy"));
            var first = Params(await page.ArgsAsync("Fetch.continueWithAuth", 2).ConfigureAwait(true));
            first.GetProperty("authChallengeResponse").GetProperty("response").GetString().ShouldBe("ProvideCredentials");

            page.Raise("Fetch.authRequired", Challenge("proxy-1", "Proxy"));
            var repeat = Params(await page.ArgsAsync("Fetch.continueWithAuth", 3).ConfigureAwait(true));
            repeat.GetProperty("authChallengeResponse").GetProperty("response").GetString().ShouldBe("CancelAuth");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static ProductionBrowserPageSource NewSource(BrowserSessionHost host, Func<string, OriginGrants>? grantsFor = null) =>
        new(host, "/fake/chromium", grantsFor ?? (static _ => new OriginGrants()), new FakeHostResolver(), new BrowserUseOptions());

    private static int ProxyPortOf(ChromiumLaunchRequest request)
    {
        var argument = request.Arguments.Single(static candidate => candidate.StartsWith(ProxyPrefix, StringComparison.Ordinal));
        return int.Parse(argument[ProxyPrefix.Length..], CultureInfo.InvariantCulture);
    }

    private static string Challenge(string requestId, string source) =>
        "{\"requestId\":\"" + requestId + "\",\"authChallenge\":{\"source\":\"" + source + "\",\"origin\":\"http://127.0.0.1:9\",\"scheme\":\"basic\",\"realm\":\"proxy\"}}";

    private static JsonElement Params(object? args) =>
        args is JsonElement element ? element : throw new InvalidOperationException("The command carried no JSON params.");

    private static async Task<bool> AcceptsConnectionAsync(int port)
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(true);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task<int> StatusCodeAsync(int port, string target, string user, string password)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(true);
        using var stream = client.GetStream();
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password));
        var request = "GET " + target + " HTTP/1.1\r\nHost: 127.0.0.1\r\nProxy-Authorization: Basic " + token + "\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request)).ConfigureAwait(true);
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var status = (await reader.ReadLineAsync().ConfigureAwait(true)) ?? string.Empty;
        return int.Parse(status.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture);
    }

    private sealed class RefusingLauncher : IChromiumLauncher
    {
        public List<ChromiumLaunchRequest> Requests { get; } = [];

        public Task<IChromiumSession> LaunchAsync(ChromiumLaunchRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromException<IChromiumSession>(new InvalidOperationException("The launch was refused."));
        }
    }

    private sealed class ScriptedPage : ICDPSession
    {
        private readonly List<(string Method, object? Args)> _commands = [];

        public List<(string Method, object? Args)> Commands => _commands;

        public ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;

        public string Id => "scripted";

        public string CloseReason => string.Empty;

        public bool Detached => false;

        /// <summary>The scripted page never disconnects, so lifecycle subscriptions are accepted and never raised.</summary>
        public event EventHandler? Disconnected
        {
            add => _ = value;
            remove => _ = value;
        }

        /// <summary>The scripted page never re-attaches, so lifecycle subscriptions are accepted and never raised.</summary>
        public event EventHandler<SessionEventArgs>? SessionAttached
        {
            add => _ = value;
            remove => _ = value;
        }

        /// <summary>The scripted page never detaches, so lifecycle subscriptions are accepted and never raised.</summary>
        public event EventHandler<SessionEventArgs>? SessionDetached
        {
            add => _ = value;
            remove => _ = value;
        }

        public event EventHandler<MessageEventArgs>? MessageReceived;

        /// <summary>Delivers one protocol event to the subscribers, as the browser would.</summary>
        public void Raise(string messageId, string json)
        {
            using var document = JsonDocument.Parse(json);
            var args = new MessageEventArgs();
            typeof(MessageEventArgs).GetProperty(nameof(MessageEventArgs.MessageID))!.SetValue(args, messageId);
            typeof(MessageEventArgs).GetProperty(nameof(MessageEventArgs.MessageData))!.SetValue(args, document.RootElement.Clone());
            MessageReceived?.Invoke(this, args);
        }

        /// <summary>Returns the arguments of the nth command with this method, waiting briefly for it to be sent.</summary>
        public async Task<object> ArgsAsync(string method, int ordinal)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Matching(method).Count < ordinal && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10).ConfigureAwait(true);
            }

            var matches = Matching(method);
            if (matches.Count < ordinal)
            {
                throw new InvalidOperationException("The command " + method + " was not sent.");
            }

            return matches[ordinal - 1] ?? throw new InvalidOperationException("The command " + method + " carried no arguments.");
        }

        public Task DetachAsync() => Task.CompletedTask;

        public void Close(string closeReason) => _ = closeReason;

        public Task<T> SendAsync<T>(string method, object? args = null, CommandOptions? options = null)
        {
            _ = options;
            _commands.Add((method, args));
            return Task.FromResult<T>(default!);
        }

        public Task<JsonElement?> SendAsync(string method, object? args = null, bool waitForCallback = true, CommandOptions? options = null)
        {
            _ = waitForCallback;
            _ = options;
            _commands.Add((method, args));
            return Task.FromResult<JsonElement?>(null);
        }

        private List<object?> Matching(string method) =>
            _commands
                .Where(command => string.Equals(command.Method, method, StringComparison.Ordinal))
                .Select(static command => command.Args)
                .ToList();
    }
}
