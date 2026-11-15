using System.Collections;
using System.Text.Json;
using Autonogrammer.Harness.BrowserUse.Egress;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>
/// The production page source. Each open gets its own <see cref="EgressGuardProxy"/>, a browser launched through it, and a
/// page whose proxy challenges are answered Chromium-side with that proxy's credential. The answer uses raw Fetch
/// (<c>Fetch.authRequired</c> / <c>Fetch.continueWithAuth</c>) on the page's CDP session. The proxy is owned by the browser
/// session and is disposed when that session closes.
/// </summary>
public sealed class ProductionBrowserPageSource : IBrowserPageSource
{
    private const string ProxyChallengeSource = "Proxy";

    private readonly BrowserSessionHost _host;
    private readonly string _executablePath;
    private readonly Func<string, OriginGrants> _grantsFor;
    private readonly IHostResolver _resolver;
    private readonly BrowserUseOptions _options;
    private readonly ITcpConnector? _connector;
    private int _opens;

    /// <summary>Initializes the source.</summary>
    /// <param name="host">The session host that owns each browser.</param>
    /// <param name="executablePath">The located Chromium binary.</param>
    /// <param name="grantsFor">Returns the session-local origin grants for a Cowork session id.</param>
    /// <param name="resolver">The resolver every proxy uses for its decision and its pinned dial.</param>
    /// <param name="options">The validated options.</param>
    /// <param name="connector">The pinned connector. Null dials with a socket.</param>
    public ProductionBrowserPageSource(
        BrowserSessionHost host,
        string executablePath,
        Func<string, OriginGrants> grantsFor,
        IHostResolver resolver,
        BrowserUseOptions options,
        ITcpConnector? connector = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        ArgumentNullException.ThrowIfNull(grantsFor);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(options);
        _host = host;
        _executablePath = executablePath;
        _grantsFor = grantsFor;
        _resolver = resolver;
        _options = options;
        _connector = connector;
    }

    /// <inheritdoc />
    public int Opens => Volatile.Read(ref _opens);

    /// <inheritdoc />
    public async ValueTask<ICDPSession> OpenAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        var proxy = CreateProxy(sessionId);
        BrowserCoworkSession? browser = null;
        var transferred = false;
        try
        {
            await proxy.StartAsync(cancellationToken).ConfigureAwait(false);
            browser = await _host.OpenAsync(_executablePath, proxy.Port, ParentEnvironment(), cancellationToken).ConfigureAwait(false);
            var page = await browser.OpenPageAsync(cancellationToken).ConfigureAwait(false);
            await AnswerProxyChallengesAsync(page, proxy).ConfigureAwait(false);
            browser.Own(proxy);
            transferred = true;
            Interlocked.Increment(ref _opens);
            return page;
        }
        catch
        {
            if (browser is not null)
            {
                await browser.DisposeAsync().ConfigureAwait(false);
            }

            if (!transferred)
            {
                await proxy.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    private EgressGuardProxy CreateProxy(string sessionId) => new(_options, _grantsFor(sessionId), _resolver, _connector);

    private static async Task AnswerProxyChallengesAsync(ICDPSession page, EgressGuardProxy proxy)
    {
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        page.MessageReceived += (_, args) =>
        {
            if (string.Equals(args.MessageID, "Fetch.requestPaused", StringComparison.Ordinal))
            {
                _ = ContinueRequestAsync(page, args.MessageData);
            }
            else if (string.Equals(args.MessageID, "Fetch.authRequired", StringComparison.Ordinal))
            {
                _ = AnswerAuthAsync(page, proxy, attempted, args.MessageData);
            }
        };
        _ = await page.SendAsync(
            "Fetch.enable",
            new { handleAuthRequests = true, patterns = new[] { new { urlPattern = "*" } } },
            waitForCallback: true,
            options: null).ConfigureAwait(false);
    }

    private static async Task ContinueRequestAsync(ICDPSession page, JsonElement data)
    {
        var requestId = ReadString(data, "requestId");
        if (string.IsNullOrEmpty(requestId))
        {
            return;
        }

        _ = await page.SendAsync("Fetch.continueRequest", new { requestId }, waitForCallback: true, options: null).ConfigureAwait(false);
    }

    private static async Task AnswerAuthAsync(ICDPSession page, EgressGuardProxy proxy, HashSet<string> attempted, JsonElement data)
    {
        var requestId = ReadString(data, "requestId");
        if (string.IsNullOrEmpty(requestId))
        {
            return;
        }

        // Only the proxy's own challenge receives the proxy credential; a website challenge is cancelled so the
        // credential never leaves for an origin. A second proxy challenge for the same request is cancelled too, so a
        // refused credential cannot loop. The decision is made before the first await, on the event thread.
        var answer = new Dictionary<string, object>(StringComparer.Ordinal) { ["response"] = "CancelAuth" };
        if (IsProxyChallenge(data) && attempted.Add(requestId))
        {
            answer = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["response"] = "ProvideCredentials",
                ["username"] = proxy.UserName,
                ["password"] = proxy.Password,
            };
        }

        _ = await page.SendAsync(
            "Fetch.continueWithAuth",
            new Dictionary<string, object>(StringComparer.Ordinal) { ["requestId"] = requestId, ["authChallengeResponse"] = answer },
            waitForCallback: true,
            options: null).ConfigureAwait(false);
    }

    private static bool IsProxyChallenge(JsonElement data) =>
        data.TryGetProperty("authChallenge", out var challenge)
        && challenge.ValueKind == JsonValueKind.Object
        && string.Equals(ReadString(challenge, "source"), ProxyChallengeSource, StringComparison.Ordinal);

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IEnumerable<KeyValuePair<string, string?>> ParentEnvironment() =>
        Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .Select(static entry => new KeyValuePair<string, string?>((string)entry.Key, entry.Value as string));
}
