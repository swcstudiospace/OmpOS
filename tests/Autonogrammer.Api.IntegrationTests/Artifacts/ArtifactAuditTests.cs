using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Autonogrammer.Api.Cowork.Artifacts;
using Autonogrammer.Api.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Artifacts;

/// <summary>Counter tags, audit payloads and the security-empty render route.</summary>
public sealed class ArtifactAuditTests
{
    /// <summary>Verifies create, update, grant and revoke emit the counter and hash-only audit events.</summary>
    [Fact]
    public async Task CreateUpdateGrantAndRevokeAreCountedWithoutSecrets()
    {
        var results = new List<string>();
        using var listener = Listen(results);
        using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        var token = TestContext.Current.CancellationToken;
        var id = "audit-" + Guid.NewGuid().ToString("N");
        using var first = ArtifactRequests.Body("hello", "Markdown");
        using var created = await client.PutAsync(new Uri("/cowork/artifacts/" + id + "/versions/1", UriKind.Relative), first, token).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var again = ArtifactRequests.Body("hello", "Markdown");
        using var updated = await client.PutAsync(new Uri("/cowork/artifacts/" + id + "/versions/1", UriKind.Relative), again, token).ConfigureAwait(true);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var version = await client.GetFromJsonAsync("/cowork/artifacts/" + id + "/versions/1", ArtifactsJsonContext.Default.ArtifactSummaryDocument, token).ConfigureAwait(true);
        version.ShouldNotBeNull();
        var grant = new ArtifactGrantDocument(id, 1, version.ContentSha256, ["https://docs.example"], "version", "web", DateTimeOffset.UnixEpoch);
        using var granted = await client.PutAsJsonAsync("/cowork/artifacts/" + id + "/versions/1/grants", grant, ArtifactsJsonContext.Default.ArtifactGrantDocument, token).ConfigureAwait(true);
        granted.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var revoke = new HttpRequestMessage(HttpMethod.Put, "/cowork/artifacts/" + id + "/versions/1/grants");
        revoke.Headers.TryAddWithoutValidation("X-Artifact-Grant", "revoke");
        using var revoked = await client.SendAsync(revoke, token).ConfigureAwait(true);
        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        listener.RecordObservableInstruments();
        results.Any(static value => string.Equals(value, "created", StringComparison.Ordinal)).ShouldBeTrue();
        results.Any(static value => string.Equals(value, "updated", StringComparison.Ordinal)).ShouldBeTrue();
        var audit = factory.Services.GetRequiredService<ArtifactAudit>();
        audit.Events.Select(static item => item.Kind).Any(static kind => string.Equals(kind, "artifact.created", StringComparison.Ordinal)).ShouldBeTrue();
        audit.Events.Single(static item => string.Equals(item.Kind, "artifact.grant", StringComparison.Ordinal)).Payload.Contains("https://docs.example", StringComparison.Ordinal).ShouldBeTrue();
        foreach (var item in audit.Events)
        {
            item.Payload.Contains("hello", StringComparison.Ordinal).ShouldBeFalse();
            item.Payload.Contains("alice", StringComparison.Ordinal).ShouldBeFalse();
        }
    }

    /// <summary>Verifies the served document lists the render route with empty security.</summary>
    [Fact]
    public async Task RenderRouteIsSecurityEmpty()
    {
        using var factory = new ApiFactory(ArtifactTestConfig.Enabled(), JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        var text = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        text.Contains("/cowork/artifact-render/{ticket}", StringComparison.Ordinal).ShouldBeTrue();
        var render = JsonDocument.Parse(text).RootElement.GetProperty("paths").GetProperty("/cowork/artifact-render/{ticket}").GetProperty("get");
        render.GetProperty("security").GetArrayLength().ShouldBe(0);
    }

    /// <summary>Verifies service limits sit at or under the store cap.</summary>
    [Fact]
    public void ServiceLimitsSitUnderTheStoreCap()
    {
        new Autonogrammer.Cowork.Artifacts.ArtifactOptions().MaxImageBytes.ShouldBeLessThanOrEqualTo(Autonogrammer.Cowork.Artifacts.Stores.ArtifactWireContract.MaxContentBytes);
        new Autonogrammer.Cowork.Artifacts.ArtifactOptions().MaxTextBytes.ShouldBeLessThanOrEqualTo(Autonogrammer.Cowork.Artifacts.Stores.ArtifactWireContract.MaxContentBytes);
    }

    private static MeterListener Listen(List<string> results)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (string.Equals(instrument.Meter.Name, ApiTelemetry.SourceName, StringComparison.Ordinal) && string.Equals(instrument.Name, ArtifactMetrics.CountName, StringComparison.Ordinal))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (string.Equals(tag.Key, "result", StringComparison.Ordinal) && tag.Value is string value)
                {
                    results.Add(value);
                }
            }
        });
        listener.Start();
        return listener;
    }
}
