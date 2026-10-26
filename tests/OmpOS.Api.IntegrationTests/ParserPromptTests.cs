using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Prompt parser wiring: the catch-all parser runs on every prompt submission, the envelope
/// kind is reported in the 202 body and the prompt_accepted event, RawText is forwarded
/// byte-for-byte, and steerable prompts never 409.
/// </summary>
public sealed class ParserPromptTests
{
    /// <summary>Verifies a magic-keyword prompt records Kind Magic in the response and the event.</summary>
    [Fact]
    public async Task Prompt_MagicKeyword_RecordsKindMagic()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var response = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"ultrathink the deployment plan\"}", testToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("intentKind").GetString().ShouldBe("Magic");

        var items = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 1, testToken).ConfigureAwait(true);
        items.Count.ShouldBe(1);
        items[0].EventType.ShouldBe("prompt_accepted");
        using var evt = JsonDocument.Parse(items[0].Data);
        evt.RootElement.GetProperty("kind").GetString().ShouldBe("prompt_accepted");
        evt.RootElement.GetProperty("prompt").GetString().ShouldBe("ultrathink the deployment plan");
        evt.RootElement.GetProperty("intentKind").GetString().ShouldBe("Magic");
        evt.RootElement.TryGetProperty("diagnosticCodes", out _).ShouldBeTrue();
    }

    /// <summary>Verifies a slash-command prompt records Kind SlashCommand.</summary>
    [Fact]
    public async Task Prompt_SlashCommand_RecordsKindSlashCommand()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var response = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"/resume from the last checkpoint\"}", testToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("intentKind").GetString().ShouldBe("SlashCommand");
    }

    /// <summary>Verifies a plain sentence falls through to Fallback and counts one fallback hit.</summary>
    [Fact]
    public async Task Prompt_PlainSentence_RecordsFallbackAndCountsMetric()
    {
        var measurements = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (string.Equals(instrument.Name, "ompos.parser.fallback.rate", StringComparison.Ordinal))
            {
                observer.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => measurements.Add(measurement));
        listener.Start();

        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var response = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"Summarize the quarterly report in plain words\"}", testToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("intentKind").GetString().ShouldBe("Fallback");

        measurements.Sum().ShouldBeGreaterThanOrEqualTo(1.0, "the plain sentence must record one fallback hit");
    }

    /// <summary>Verifies steering text during a running prompt is accepted, not rejected with 409.</summary>
    [Fact]
    public async Task Prompt_SteerDuringRunningPrompt_AcceptedNotConflict()
    {
        var gate = new GatedHarnessFactory();
        using var factory = new OmpApiFactory(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(gate));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var first = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"start the long task\"}", testToken).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using var second = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"keep going faster\"}", testToken).ConfigureAwait(true);
        second.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await second.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("intentKind").GetString().ShouldBe("Steer");

        gate.Release();
        var settled = await SessionClient.WaitForStatusAsync(client, id, Contracts.Sessions.SessionStatus.Completed, TimeSpan.FromSeconds(15), testToken).ConfigureAwait(true);
        settled.Messages.Select(static message => message.Role).ShouldBe(["user", "steer"]);
    }

    /// <summary>Verifies an empty prompt still returns 400 after the parser wiring.</summary>
    [Fact]
    public async Task Prompt_EmptyPrompt_Returns400()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var response = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"\"}", testToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
