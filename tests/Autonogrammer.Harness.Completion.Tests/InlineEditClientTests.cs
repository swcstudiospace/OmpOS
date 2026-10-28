using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Completion.Tests;

/// <summary>
/// Verifies the inline edit provider client (ADE-12) against a local stub endpoint: what it sends, that it sends nothing
/// without a provider, and how a recorded reply becomes a proposal.
/// </summary>
public sealed class InlineEditClientTests
{
    /// <summary>ADE-12: the request carries the selected range and the instruction, and nothing else, to the configured endpoint.</summary>
    [Fact]
    public async Task SendsTheSelectedRangeAndInstructionToTheConfiguredProvider()
    {
        await using var provider = StubCompletionProvider.Start("""{"replacement":"unused"}""");
        using var http = new HttpClient();
        var client = new InlineEditClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);

        await client
            .ProposeAsync("var x = 1;", "rename x to count", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        provider.RequestCount.ShouldBe(1);
        var request = provider.Requests.Single();
        request.Method.ShouldBe("POST");
        request.ContentType.ShouldStartWith("application/json");

        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.EnumerateObject().Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ShouldBe(["instruction", "selection"]);
        body.RootElement.GetProperty("selection").GetString().ShouldBe("var x = 1;");
        body.RootElement.GetProperty("instruction").GetString().ShouldBe("rename x to count");
    }

    /// <summary>ADE-12: with no provider configured the client is disabled and sends no request.</summary>
    [Fact]
    public async Task NoProviderSendsNothing()
    {
        await using var provider = StubCompletionProvider.Start("""{"replacement":"unused"}""");
        using var http = new HttpClient();
        var client = new InlineEditClient(CompletionSettings.None, http);

        var result = await client
            .ProposeAsync("var x = 1;", "rename x", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Outcome.ShouldBe(InlineEditOutcome.Disabled);
        result.Replacement.ShouldBeEmpty();
        provider.RequestCount.ShouldBe(0);
    }

    /// <summary>ADE-12: a recorded provider reply becomes a proposal for the caller to review.</summary>
    [Fact]
    public async Task RecordedReplyBecomesAProposal()
    {
        await using var provider = StubCompletionProvider.Start("""{"replacement":"var count = 1;"}""");
        using var http = new HttpClient();
        var client = new InlineEditClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);

        var result = await client
            .ProposeAsync("var x = 1;", "rename x to count", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Outcome.ShouldBe(InlineEditOutcome.Proposed);
        result.Replacement.ShouldBe("var count = 1;");
    }

    /// <summary>An empty replacement is a proposal to delete the selection, not a missing proposal.</summary>
    [Fact]
    public async Task EmptyReplacementProposesDeletingTheSelection()
    {
        await using var provider = StubCompletionProvider.Start("""{"replacement":""}""");
        using var http = new HttpClient();
        var client = new InlineEditClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);

        var result = await client
            .ProposeAsync("// obsolete", "delete this", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Outcome.ShouldBe(InlineEditOutcome.Proposed);
        result.Replacement.ShouldBeEmpty();
    }

    /// <summary>A reply without a replacement is no proposal.</summary>
    [Fact]
    public async Task ReplyWithoutAReplacementIsNoProposal()
    {
        await using var provider = StubCompletionProvider.Start("{}");
        using var http = new HttpClient();
        var client = new InlineEditClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);

        var result = await client
            .ProposeAsync("var x = 1;", "rename x", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Outcome.ShouldBe(InlineEditOutcome.NoProposal);
        result.Replacement.ShouldBeEmpty();
    }

    /// <summary>A non-success status is raised to the caller and is not turned into a proposal.</summary>
    [Fact]
    public async Task ServerErrorRaisesHttpRequestException()
    {
        await using var provider = StubCompletionProvider.Start("""{"replacement":"x"}""", HttpStatusCode.InternalServerError);
        using var http = new HttpClient();
        var client = new InlineEditClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);

        await Should.ThrowAsync<HttpRequestException>(async () =>
            await client.ProposeAsync("var x = 1;", "rename x", TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ConfigureAwait(true);

        provider.RequestCount.ShouldBe(1);
    }

    /// <summary>An empty selection or a blank instruction is rejected before any request is sent.</summary>
    [Fact]
    public async Task EmptySelectionOrBlankInstructionIsRejectedWithoutARequest()
    {
        await using var provider = StubCompletionProvider.Start("""{"replacement":"x"}""");
        using var http = new HttpClient();
        var client = new InlineEditClient(CompletionSettings.ForEndpoint(provider.Endpoint), http);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await client.ProposeAsync(string.Empty, "rename x", TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(async () =>
            await client.ProposeAsync("var x = 1;", "   ", TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ConfigureAwait(true);

        provider.RequestCount.ShouldBe(0);
    }
}
