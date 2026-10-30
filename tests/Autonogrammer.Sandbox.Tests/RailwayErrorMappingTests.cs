using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Tests for how <see cref="RailwayCloudEnvironmentClient"/> maps Railway HTTP and GraphQL failures to
/// <see cref="CloudEnvironmentException"/>. Every case drives <c>FindContainerAsync</c>, whose first request is <c>projectServices</c>.
/// </summary>
public sealed class RailwayErrorMappingTests
{
    [Fact]
    public async Task RateLimitCarriesTheRetryAfterSeconds()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(HttpStatusCode.TooManyRequests, "{}", TimeSpan.FromSeconds(7)));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.RateLimited);
        failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(7));
        failure.Operation.ShouldBe("projectServices");
    }

    [Fact]
    public async Task RateLimitToleratesAnHttpDateRelativeToTheClock()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () =>
        {
            var response = RailwayFixtureHandler.Json(HttpStatusCode.TooManyRequests, "{}");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(harness.Clock.GetUtcNow().AddSeconds(30));
            return response;
        });

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.RateLimited);
        failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task RateLimitWithoutAHeaderHasNoRetryAfter()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(HttpStatusCode.TooManyRequests, "{}"));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.RateLimited);
        failure.RetryAfter.ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task RejectedCredentialsAreUnauthorized(HttpStatusCode status)
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(status, "{}"));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.Unauthorized);
    }

    [Fact]
    public async Task NotAuthorizedInA200ErrorsArrayIsUnauthorizedWithTheTraceId()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(
            HttpStatusCode.OK,
            "{\"errors\":[{\"message\":\"Not Authorized\",\"extensions\":{\"code\":\"INTERNAL_SERVER_ERROR\"},\"traceId\":\"trace-401\"}],\"data\":null}"));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.Unauthorized);
        failure.TraceId.ShouldBe("trace-401");
    }

    [Fact]
    public async Task OtherErrorsIn200AreGraphQlErrorsWithTheFirstMessageCodeAndTraceId()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(
            HttpStatusCode.OK,
            "{\"errors\":[{\"message\":\"Problem processing request\",\"extensions\":{\"code\":\"INTERNAL_SERVER_ERROR\"},\"traceId\":\"trace-1\"},{\"message\":\"second\"}]}"));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.GraphQlError);
        failure.Message.ShouldContain("Problem processing request", Case.Sensitive);
        failure.Message.ShouldContain("INTERNAL_SERVER_ERROR", Case.Sensitive);
        failure.Message.ShouldNotContain("second", Case.Sensitive);
        failure.TraceId.ShouldBe("trace-1");
    }

    [Fact]
    public async Task TheTraceIdIsAlsoReadFromTheErrorExtensions()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(
            HttpStatusCode.OK,
            "{\"errors\":[{\"message\":\"boom\",\"extensions\":{\"code\":\"X\",\"traceId\":\"trace-ext\"}}]}"));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.TraceId.ShouldBe("trace-ext");
    }

    [Fact]
    public async Task ValidationFailureAt400IsAGraphQlErrorWithItsCode()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(
            HttpStatusCode.BadRequest,
            "{\"errors\":[{\"message\":\"Cannot query field \\\"nope\\\"\",\"extensions\":{\"code\":\"GRAPHQL_VALIDATION_FAILED\"},\"traceId\":\"trace-400\"}]}"));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.GraphQlError);
        failure.Message.ShouldContain("GRAPHQL_VALIDATION_FAILED", Case.Sensitive);
        failure.TraceId.ShouldBe("trace-400");
    }

    [Fact]
    public async Task A400WithAnUnreadableBodyIsStillAGraphQlError()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(HttpStatusCode.BadRequest, "<html>bad request</html>"));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.GraphQlError);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ServerErrorsAndUnexpectedStatusesAreTransportFailures(HttpStatusCode status)
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(status, "{}"));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.Transport);
        failure.Message.ShouldContain(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), Case.Sensitive);
    }

    [Fact]
    public async Task ANetworkErrorIsATransportFailureThatKeepsTheInnerException()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => throw new HttpRequestException("name resolution failed"));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.Transport);
        failure.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task APerRequestTimeoutIsATransportFailure()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => throw new TaskCanceledException("timed out"));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.Transport);
        failure.Message.ShouldContain("timed out", Case.Sensitive);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"data\":")]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    public async Task MalformedOrEmptyBodiesAreInvalidResponses(string body)
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(HttpStatusCode.OK, body));

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.InvalidResponse);
    }

    [Fact]
    public async Task ABodyOverTheFourMebibyteCapIsAnInvalidResponse()
    {
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('x', (4 * 1024 * 1024) + 1), Encoding.UTF8, "application/json"),
        });

        var failure = await FindFailureAsync(harness).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.InvalidResponse);
        failure.InnerException.ShouldBeOfType<InvalidDataException>();
    }

    [Fact]
    public async Task ACancelledCallPropagatesCancellationInsteadOfATransportFailure()
    {
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        var thrown = await Record.ExceptionAsync(
            async () => await harness.Client.FindContainerAsync("s1", cancelled.Token).ConfigureAwait(true)).ConfigureAwait(true);

        thrown.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task AMutationThatGotAServerErrorIsSentExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services());
        harness.Api.Add("serviceCreate", () => RailwayFixtureHandler.Json(HttpStatusCode.BadGateway, "{}"));
        harness.Api.Data("serviceCreate", "{\"serviceCreate\":{\"id\":\"svc-1\",\"name\":\"x\"}}");

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.Transport);
        failure.Operation.ShouldBe("serviceCreate");
        harness.Api.CountOf("serviceCreate").ShouldBe(1);
    }

    [Fact]
    public async Task AServiceCreateAnswerWithoutAnIdIsAnInvalidResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services());
        harness.Api.Data("serviceCreate", "{\"serviceCreate\":{\"name\":\"x\"}}");

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.InvalidResponse);
        harness.Api.CountOf("variableCollectionUpsert").ShouldBe(0);
    }

    private static async Task<CloudEnvironmentException> FindFailureAsync(RailwayHarness harness) =>
        await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.FindContainerAsync("s1", TestContext.Current.CancellationToken).ConfigureAwait(true)).ConfigureAwait(true);
}
