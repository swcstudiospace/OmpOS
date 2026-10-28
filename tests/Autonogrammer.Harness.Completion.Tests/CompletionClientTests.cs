using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Completion.Tests;

/// <summary>
/// Verifies inline completion (ADE-13) against a local stub endpoint: no request without a provider, exactly one request
/// with one, and the suggestion that comes back.
/// </summary>
public sealed class CompletionClientTests
{
    /// <summary>ADE-13: without a configured provider, completion is off and the client sends no request.</summary>
    [Fact]
    public async Task NoProvider_ReturnsDisabled_AndSendsNoRequest()
    {
        await using var provider = StubCompletionProvider.Start("""{"suggestion":"unused"}""");
        using var http = new HttpClient();
        var client = new CompletionClient(CompletionSettings.None, http);

        var result = await client.SuggestAsync(new CaretBuffer("hello world", 5), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CompletionStatus.Disabled);
        result.Text.ShouldBeEmpty();
        provider.RequestCount.ShouldBe(0);
    }

    /// <summary>ADE-13: with a provider configured, one request is posted and its suggestion is returned at the caret.</summary>
    [Fact]
    public async Task Provider_PostsExactlyOneRequest_AndReturnsSuggestion()
    {
        await using var provider = StubCompletionProvider.Start("""{"suggestion":", big"}""");
        using var http = new HttpClient();
        var client = new CompletionClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);

        var result = await client.SuggestAsync(new CaretBuffer("hello world", 5), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CompletionStatus.Suggested);
        result.Text.ShouldBe(", big");
        provider.RequestCount.ShouldBe(1);

        var request = provider.Requests.Single();
        request.Method.ShouldBe("POST");
        request.ContentType.ShouldStartWith("application/json");

        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetProperty("prefix").GetString().ShouldBe("hello");
        body.RootElement.GetProperty("suffix").GetString().ShouldBe(" world");
    }

    /// <summary>ADE-13: accepting the suggestion inserts its text at the caret.</summary>
    [Fact]
    public async Task Provider_AcceptingSuggestion_InsertsTextAtCaret()
    {
        await using var provider = StubCompletionProvider.Start("""{"suggestion":", big"}""");
        using var http = new HttpClient();
        var client = new CompletionClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);
        var buffer = new CaretBuffer("hello world", 5);

        var result = await client.SuggestAsync(buffer, TestContext.Current.CancellationToken);
        var accepted = CompletionAcceptance.Accept(buffer, result);

        accepted.Text.ShouldBe("hello, big world");
        accepted.Caret.ShouldBe(10);
    }

    /// <summary>ADE-13: dismissing the suggestion leaves the buffer as it was.</summary>
    [Fact]
    public async Task Provider_DismissingSuggestion_InsertsNothing()
    {
        await using var provider = StubCompletionProvider.Start("""{"suggestion":", big"}""");
        using var http = new HttpClient();
        var client = new CompletionClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);
        var buffer = new CaretBuffer("hello world", 5);

        var result = await client.SuggestAsync(buffer, TestContext.Current.CancellationToken);
        var dismissed = CompletionAcceptance.Dismiss(buffer);

        result.Status.ShouldBe(CompletionStatus.Suggested);
        dismissed.Text.ShouldBe("hello world");
        dismissed.Caret.ShouldBe(5);
        provider.RequestCount.ShouldBe(1);
    }

    /// <summary>ADE-13: a provider that answers with no suggestion yields NoSuggestion, not an empty insertion.</summary>
    [Theory]
    [InlineData("""{"suggestion":""}""")]
    [InlineData("{}")]
    public async Task Provider_WithoutSuggestion_ReturnsNoSuggestion(string responseBody)
    {
        await using var provider = StubCompletionProvider.Start(responseBody);
        using var http = new HttpClient();
        var client = new CompletionClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);

        var result = await client.SuggestAsync(new CaretBuffer("hello", 5), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CompletionStatus.NoSuggestion);
        result.Text.ShouldBeEmpty();
        provider.RequestCount.ShouldBe(1);
    }

    /// <summary>ADE-13: a provider error is raised to the caller rather than shown as a suggestion.</summary>
    [Fact]
    public async Task Provider_ErrorStatus_Throws()
    {
        await using var provider = StubCompletionProvider.Start("{}", HttpStatusCode.InternalServerError);
        using var http = new HttpClient();
        var client = new CompletionClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);

        await Should.ThrowAsync<HttpRequestException>(
            () => client.SuggestAsync(new CaretBuffer("hello", 5), TestContext.Current.CancellationToken));
        provider.RequestCount.ShouldBe(1);
    }

    /// <summary>ADE-13: settings without an endpoint are not configured.</summary>
    [Fact]
    public void Settings_None_IsNotConfigured()
    {
        CompletionSettings.None.IsConfigured.ShouldBeFalse();
        CompletionSettings.None.Endpoint.ShouldBeNull();
    }

    /// <summary>ADE-13: an endpoint must be an absolute http or https URI.</summary>
    [Theory]
    [InlineData("ftp://example.com/complete")]
    [InlineData("file:///tmp/complete")]
    public void Settings_EndpointNotHttp_Throws(string endpoint)
    {
        Should.Throw<ArgumentException>(() => CompletionSettings.ForEndpoint(new Uri(endpoint)));
    }

    /// <summary>ADE-13: a relative endpoint is rejected.</summary>
    [Fact]
    public void Settings_RelativeEndpoint_Throws()
    {
        Should.Throw<ArgumentException>(() => CompletionSettings.ForEndpoint(new Uri("/complete", UriKind.Relative)));
    }
}
