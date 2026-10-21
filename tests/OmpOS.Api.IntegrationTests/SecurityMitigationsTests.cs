using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Phase 2 security mitigations: CORS default deny, correlation id validation,
/// and ProblemDetails bodies without stack traces.
/// </summary>
public sealed class SecurityMitigationsTests
{
    /// <summary>
    /// Verifies the default configuration (empty Api:Cors:Origins) grants nothing:
    /// a cross-origin simple request gets no allow-origin header and a preflight is not granted.
    /// </summary>
    [Fact]
    public async Task Cors_DefaultDeny_BlocksSimpleAndPreflight()
    {
        // Default appsettings.json ships Api:Cors:Origins as an empty list.
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var simple = new HttpRequestMessage(HttpMethod.Get, new Uri("/sessions", UriKind.Relative));
        simple.Headers.Add("Origin", "https://evil.example");
        using var simpleResponse = await client.SendAsync(simple, testToken);
        simpleResponse.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();

        using var preflight = new HttpRequestMessage(HttpMethod.Options, new Uri("/sessions", UriKind.Relative));
        preflight.Headers.Add("Origin", "https://evil.example");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        using var preflightResponse = await client.SendAsync(preflight, testToken);
        preflightResponse.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    /// <summary>Verifies a valid Guid correlation id is echoed back in the response header.</summary>
    [Fact]
    public async Task CorrelationId_ValidGuid_IsEchoed()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var correlation = Guid.NewGuid().ToString("N");

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health/live", UriKind.Relative));
        request.Headers.Add("x-correlation-id", correlation);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.GetValues("x-correlation-id").Contains(correlation, StringComparer.Ordinal).ShouldBeTrue();
    }

    /// <summary>Verifies a 129-character inbound id is replaced by a minted Guid.</summary>
    [Fact]
    public async Task CorrelationId_OversizedValue_IsMinted()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var oversized = new string('x', 129);
        oversized.Length.ShouldBe(129);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health/live", UriKind.Relative));
        request.Headers.Add("x-correlation-id", oversized);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var echoed = string.Join(",", response.Headers.GetValues("x-correlation-id"));
        string.Equals(echoed, oversized, StringComparison.Ordinal).ShouldBeFalse("a 129-character inbound id must be replaced with a minted one");
        Guid.TryParse(echoed, out _).ShouldBeTrue("a 129-character inbound id must be replaced with a minted guid");
    }

    /// <summary>Verifies a 20-character inbound id containing a space is replaced by a minted Guid.</summary>
    [Fact]
    public async Task CorrelationId_NonGuidValue_IsMinted()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        const string Inbound = "1234567890 123456789";
        Inbound.Length.ShouldBe(20);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health/live", UriKind.Relative));
        request.Headers.Add("x-correlation-id", Inbound);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var echoed = string.Join(",", response.Headers.GetValues("x-correlation-id"));
        string.Equals(echoed, Inbound, StringComparison.Ordinal).ShouldBeFalse("an invalid inbound id must be replaced with a minted one");
        Guid.TryParse(echoed, out _).ShouldBeTrue("an invalid inbound id must be replaced with a minted guid");
    }

    /// <summary>
    /// Verifies 400 responses carry a ProblemDetails body whose parsed properties contain
    /// no stack-frame lines and no exception type names.
    /// </summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("{\"prompt\":\"\"}")]
    public async Task ProblemDetails_BadRequest_HidesInternals(string body)
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var response = await SessionClient.PostPromptAsync(client, id, body, testToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var payload = await response.Content.ReadAsStringAsync(testToken);
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().ShouldBe(400);
        var text = string.Join(
            "\n",
            root.GetProperty("title").GetString() ?? string.Empty,
            root.GetProperty("detail").GetString() ?? string.Empty);
        text.Split('\n').ShouldAllBe(static line => !line.TrimStart().StartsWith("at ", StringComparison.Ordinal));
        text.ShouldNotContain("System.");
        text.ShouldNotContain("Exception");
    }

    /// <summary>
    /// Verifies a parsable-but-oversized inbound id is replaced by a minted Guid.
    /// </summary>
    /// <remarks>
    /// <see cref="Guid.TryParse(string?, out Guid)"/> ignores leading and trailing whitespace,
    /// so a 32-character Guid padded with spaces to 129 characters still parses: this input
    /// reaches the length arm of the correlation validation. Without that arm the padded
    /// value would be echoed back unchanged.
    /// </remarks>
    [Fact]
    public async Task CorrelationId_ParsableButOversizedValue_IsMinted()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var padded = new string(' ', 97) + Guid.NewGuid().ToString("N");
        padded.Length.ShouldBe(129);
        Guid.TryParse(padded, out _).ShouldBeTrue("the padded value must still parse as a Guid to reach the length branch");

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health/live", UriKind.Relative));
        request.Headers.Add("x-correlation-id", padded);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var echoed = string.Join(",", response.Headers.GetValues("x-correlation-id"));
        string.Equals(echoed, padded, StringComparison.Ordinal).ShouldBeFalse("a parsable 129-character inbound id must be replaced with a minted one");
        Guid.TryParse(echoed, out _).ShouldBeTrue("a parsable 129-character inbound id must be replaced with a minted guid");
    }

    /// <summary>
    /// Verifies an unhandled exception carries a ProblemDetails 500 body whose parsed properties
    /// contain no stack-frame lines, no exception message, and no exception type name.
    /// </summary>
    [Fact]
    public async Task ProblemDetails_UnhandledException_HidesInternals()
    {
        // Production-only: Development keeps the developer exception page by design, so this
        // test opts this host into Production where UseExceptionHandler owns the 500 path.
        using var factory = new OmpApiFactory(
            environment: "Production",
            configureServices: static services =>
        {
            services.AddSingleton<IStartupFilter>(new UnhandledExceptionStartupFilter());
        });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(new Uri(UnhandledExceptionStartupFilter.BoomPath, UriKind.Relative), testToken);
        var payload = await response.Content.ReadAsStringAsync(testToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().ShouldBe(500);
        var text = string.Join(
            "\n",
            root.GetProperty("title").GetString() ?? string.Empty,
            root.GetProperty("detail").GetString() ?? string.Empty);
        text.Split('\n').ShouldAllBe(static line => !line.TrimStart().StartsWith("at ", StringComparison.Ordinal));
        text.ShouldNotContain(UnhandledExceptionStartupFilter.BoomMarker);
        text.ShouldNotContain("InvalidOperationException");
    }

    /// <summary>
    /// Verifies an unknown session id returns 404 without internals: an empty body
    /// is accepted, otherwise the problem payload parses and its text holds no
    /// stack-frame lines, exception type names, or stack-trace markers.
    /// </summary>
    [Fact]
    public async Task ProblemDetails_NotFound_HidesInternals()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(new Uri("/sessions/does-not-exist", UriKind.Relative), testToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await response.Content.ReadAsStringAsync(testToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        string text;
        if (string.Equals(response.Content.Headers.ContentType?.MediaType, "application/problem+json", StringComparison.Ordinal))
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            root.GetProperty("status").GetInt32().ShouldBe(404);
            text = string.Join(
                "\n",
                root.GetProperty("title").GetString() ?? string.Empty,
                root.GetProperty("detail").GetString() ?? string.Empty);
        }
        else
        {
            text = payload;
        }

        text.Split('\n').ShouldAllBe(static line => !line.TrimStart().StartsWith("at ", StringComparison.Ordinal));
        text.ShouldNotContain("System.");
        text.ShouldNotContain("Exception");
        text.Contains("StackTrace", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
        text.Contains("Stack Trace", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies an unsupported content type returns 415 without internals: an empty
    /// body is accepted, otherwise the problem payload parses and its text holds no
    /// stack-frame lines, exception type names, or stack-trace markers.
    /// </summary>
    [Fact]
    public async Task ProblemDetails_UnsupportedMediaType_HidesInternals()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var response = await SessionClient.PostPromptAsync(client, id, "hello", testToken, "text/plain");
        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        var payload = await response.Content.ReadAsStringAsync(testToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        string text;
        if (string.Equals(response.Content.Headers.ContentType?.MediaType, "application/problem+json", StringComparison.Ordinal))
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            root.GetProperty("status").GetInt32().ShouldBe(415);
            text = string.Join(
                "\n",
                root.GetProperty("title").GetString() ?? string.Empty,
                root.GetProperty("detail").GetString() ?? string.Empty);
        }
        else
        {
            text = payload;
        }

        text.Split('\n').ShouldAllBe(static line => !line.TrimStart().StartsWith("at ", StringComparison.Ordinal));
        text.ShouldNotContain("System.");
        text.ShouldNotContain("Exception");
        text.Contains("StackTrace", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
        text.Contains("Stack Trace", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }
}
