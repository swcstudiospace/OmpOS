using System.Net;
using System.Security.Authentication;
using OmpOS.Harness.OmpConfig;
using OmpOS.Harness.Providers;
using OmpOS.Native.Settings;
using OmpOS.Native.Tests.AgentPanel.Views;
using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>The diagnostics run of the Network page over a fake handler: what is checked, what each answer means, and what is never sent.</summary>
public sealed class NetworkDiagnosticsSourceTests
{
    private static ProviderEntry Entry(string id, string url) => new(
        id,
        ProviderKind.ApiKey,
        ProviderTransport.OpenAiCompatible,
        ProviderAuthType.BearerToken,
        new Uri(url),
        ProviderCapabilities.ChatCompletions,
        null,
        default);

    private static string Key(Uri uri) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{uri.Host}:{uri.Port}");

    private static OmpManagedProvider Managed(string id, string url) =>
        new(id, new Uri(url), OmpProviderApi.OpenAiCompletions, "MANAGED_KEY", NoAuthentication: false, [new OmpManagedModel("m")]);

    private static NetworkDiagnosticsSource Source(
        RecordingHandler handler,
        IReadOnlyList<ProviderEntry>? catalogue = null,
        IReadOnlyList<OmpManagedProvider>? managed = null,
        TimeProvider? time = null) =>
        new(
            _ => Task.FromResult(managed ?? []),
            () => new HttpClient(handler, disposeHandler: false),
            time,
            catalogue: catalogue ?? []);

    /// <summary>Any HTTP status, an error status included, makes the endpoint reachable and is shown as the status.</summary>
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    public async Task AnyHttpStatusIsReachableAndShown(HttpStatusCode status)
    {
        using var handler = RecordingHandler.Always(status);
        var source = Source(handler, [Entry("one", "https://api.one.example/v1")]);

        var report = await source.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var check = report.Checks.ShouldHaveSingleItem();
        check.Status.ShouldBe(EndpointStatus.Reachable);
        check.HttpStatus.ShouldBe((int)status);
        check.Name.ShouldBe("one");
        check.Host.ShouldBe("api.one.example");
        check.Latency.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
        check.Message.ShouldBe(NetworkCopy.CheckMessage(EndpointStatus.Reachable, (int)status));
    }

    /// <summary>A name that does not resolve, a refused connection, a failed secure connection and a certificate fault each get their own status.</summary>
    [Fact]
    public async Task FailuresAreClassified()
    {
        using var handler = new RecordingHandler((request, _) =>
        {
            Exception fault = request.RequestUri!.Host switch
            {
                "dns.example" => new HttpRequestException(HttpRequestError.NameResolutionError, "no such host"),
                "refused.example" => new HttpRequestException(HttpRequestError.ConnectionError, "refused"),
                "tls.example" => new HttpRequestException(HttpRequestError.SecureConnectionError, "handshake"),
                "cert.example" => new HttpRequestException("wrapped", new AuthenticationException("bad certificate")),
                _ => new InvalidOperationException("unexpected host"),
            };
            return Task.FromException<HttpResponseMessage>(fault);
        });
        var source = Source(
            handler,
            [
                Entry("dns", "https://dns.example/v1"),
                Entry("refused", "http://refused.example:8080/v1"),
                Entry("tls", "https://tls.example/v1"),
                Entry("cert", "https://cert.example/v1"),
            ]);

        var report = await source.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        report.Checks.Select(static check => check.Status).ToArray().ShouldBe(
            [EndpointStatus.Unreachable, EndpointStatus.Unreachable, EndpointStatus.TlsFailed, EndpointStatus.TlsFailed]);
        report.Checks.ShouldAllBe(static check => check.HttpStatus == null);
        report.Checks[1].Host.ShouldBe("refused.example:8080");
        report.Checks.ShouldAllBe(static check => !check.Message.Contains("handshake", StringComparison.Ordinal) && !check.Message.Contains("wrapped", StringComparison.Ordinal));
    }

    /// <summary>An endpoint that does not answer in five seconds is timed out when the clock passes the limit; the others in the run are unaffected.</summary>
    [Fact]
    public async Task ASlowEndpointTimesOutAtTheLimit()
    {
        var token = TestContext.Current.CancellationToken;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (request, cancellationToken) =>
        {
            if (string.Equals(request.RequestUri!.Host, "slow.example", StringComparison.Ordinal))
            {
                _ = started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var clock = new ManualTimeProvider();
        var source = Source(handler, [Entry("fast", "https://fast.example/v1"), Entry("slow", "https://slow.example/v1")], time: clock);

        var run = source.RunAsync(token);
        await started.Task.WaitAsync(token).ConfigureAwait(true);
        clock.Advance(TimeSpan.FromSeconds(4));
        run.IsCompleted.ShouldBeFalse("the limit is five seconds");
        clock.Advance(TimeSpan.FromSeconds(1));
        var report = await run.ConfigureAwait(true);

        report.Checks.Select(static check => check.Status).ToArray().ShouldBe([EndpointStatus.Reachable, EndpointStatus.TimedOut]);
        report.Checks[1].HttpStatus.ShouldBeNull();
        report.At.ShouldBe(clock.GetUtcNow());
        NetworkDiagnosticsSource.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(5));
    }

    /// <summary>A caller that cancels stops the run with a cancellation, not with a timed-out result.</summary>
    [Fact]
    public async Task CancellingTheRunThrowsInsteadOfReportingATimeout()
    {
        using var cancel = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var source = Source(handler, [Entry("slow", "https://slow.example/v1")]);

        var run = Record.ExceptionAsync(() => source.RunAsync(cancel.Token));
        await started.Task.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await cancel.CancelAsync().ConfigureAwait(true);
        var outcome = await run.ConfigureAwait(true);

        outcome.ShouldBeAssignableTo<OperationCanceledException>();
    }

    /// <summary>Endpoints are deduplicated by host and port; the managed provider is listed first and keeps the name; a different port is a different endpoint.</summary>
    [Fact]
    public async Task EndpointsAreDeduplicatedByHostAndPort()
    {
        using var handler = RecordingHandler.Always(HttpStatusCode.OK);
        var source = Source(
            handler,
            [
                Entry("catalogue-same", "https://API.Shared.example/v2"),
                Entry("catalogue-other-port", "https://api.shared.example:8443/v1"),
                Entry("catalogue-twice", "https://api.other.example/v1"),
                Entry("catalogue-twice-again", "https://api.other.example/v1/"),
            ],
            [Managed("managed", "https://api.shared.example/v1")]);

        var report = await source.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        report.Checks.Select(static check => check.Name).ToArray().ShouldBe(["managed", "catalogue-other-port", "catalogue-twice"]);
        report.Checks.Select(static check => check.Host).ToArray().ShouldBe(["api.shared.example", "api.shared.example:8443", "api.other.example"]);
        handler.Requests.Count.ShouldBe(3);
    }

    /// <summary>Each endpoint gets one plain GET: no header, no body, no user information, no query, no fragment, whatever the configured address held.</summary>
    [Fact]
    public async Task EachRequestIsOnePlainGetWithNoCredentials()
    {
        using var handler = RecordingHandler.Always(HttpStatusCode.OK);
        var source = Source(
            handler,
            [Entry("catalogue", "http://localhost:11434/v1/")],
            [Managed("managed", "https://user:hunter2@managed.example:8443/v1?api_key=abc#frag")]);

        var report = await source.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        handler.Requests.Count.ShouldBe(2, "one request per endpoint");
        foreach (var request in handler.Requests)
        {
            request.Method.ShouldBe(HttpMethod.Get);
            request.HeaderNames.ShouldBeEmpty("no Authorization, no key header, no cookie");
            request.HasContent.ShouldBeFalse();
            request.Uri.UserInfo.ShouldBeEmpty();
            request.Uri.Query.ShouldBeEmpty();
            request.Uri.Fragment.ShouldBeEmpty();
        }

        handler.Requests.Single(static request => string.Equals(request.Uri.Host, "managed.example", StringComparison.Ordinal)).Uri.AbsoluteUri.ShouldBe("https://managed.example:8443/v1");
        report.Checks.ShouldAllBe(static check => !check.Host.Contains('@', StringComparison.Ordinal)
            && !check.Host.Contains("hunter2", StringComparison.Ordinal)
            && !check.Host.Contains('?', StringComparison.Ordinal)
            && !check.Message.Contains("hunter2", StringComparison.Ordinal));
        report.Checks[0].Host.ShouldBe("managed.example:8443");
    }

    /// <summary>With the shipped catalogue every row that has an http or https endpoint is checked once per host and port, and nothing else is.</summary>
    [Fact]
    public async Task TheShippedCatalogueIsCheckedOncePerHostAndPort()
    {
        using var handler = RecordingHandler.Always(HttpStatusCode.OK);
        var source = new NetworkDiagnosticsSource(clientFactory: () => new HttpClient(handler, disposeHandler: false));

        var report = await source.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var expected = ProviderCatalogue.All
            .Where(static entry => entry.BaseEndpoint is { Scheme: "http" or "https" })
            .Select(static entry => Key(entry.BaseEndpoint!))
            .ToHashSet(StringComparer.Ordinal);
        expected.Count.ShouldBeGreaterThan(5);
        report.Checks.Count.ShouldBe(expected.Count);
        handler.Requests.Select(static request => Key(request.Uri)).ToHashSet(StringComparer.Ordinal).ShouldBe(expected, ignoreOrder: true);
        handler.Requests.ShouldAllBe(static request => request.HeaderNames.Count == 0 && request.Method == HttpMethod.Get);
    }

    /// <summary>A models.yml that cannot be read leaves the catalogue endpoints; an address that is not http or https is not requested.</summary>
    [Fact]
    public async Task AnUnreadableManagedFileAndOddSchemesAreSkipped()
    {
        using var handler = RecordingHandler.Always(HttpStatusCode.OK);
        var source = new NetworkDiagnosticsSource(
            static _ => throw new IOException("models.yml is locked"),
            () => new HttpClient(handler, disposeHandler: false),
            catalogue: [Entry("ftp", "ftp://files.example/"), Entry("ok", "https://ok.example/v1")]);

        var report = await source.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        report.Checks.Select(static check => check.Name).ToArray().ShouldBe(["ok"]);
    }

    /// <summary>Nothing is requested before the run: building the source and reading nothing sends nothing.</summary>
    [Fact]
    public void ConstructionSendsNothing()
    {
        using var handler = RecordingHandler.Always(HttpStatusCode.OK);
        var created = 0;

        _ = new NetworkDiagnosticsSource(clientFactory: () =>
        {
            created++;
            return new HttpClient(handler, disposeHandler: false);
        });

        created.ShouldBe(0);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>The handler a real run uses follows at most three redirects, keeps no cookies and carries no credentials.</summary>
    [Fact]
    public void TheDefaultHandlerFollowsThreeRedirectsAndCarriesNoCredentials()
    {
        using var handler = NetworkDiagnosticsSource.CreateDefaultHandler();

        handler.AllowAutoRedirect.ShouldBeTrue();
        handler.MaxAutomaticRedirections.ShouldBe(3);
        NetworkDiagnosticsSource.MaxRedirects.ShouldBe(3);
        handler.UseCookies.ShouldBeFalse();
        handler.PreAuthenticate.ShouldBeFalse();
        handler.Credentials.ShouldBeNull();
        handler.DefaultProxyCredentials.ShouldBeNull();
        using var client = NetworkDiagnosticsSource.CreateDefaultClient();
        client.DefaultRequestHeaders.Count().ShouldBe(0);
    }

    /// <summary>A time limit that is not positive is refused.</summary>
    [Fact]
    public void ANonPositiveTimeLimitIsRefused() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkDiagnosticsSource(timeout: TimeSpan.Zero));
}
