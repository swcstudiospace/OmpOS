using System.Text.Json;
using Autonogrammer.Web.Demo;
using Autonogrammer.Web.Services;
using Autonogrammer.Web.Services.Artifacts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>
/// Every route the artifact client calls is an operation documented in docs/api/openapi.json, and the Demo answers each call in the
/// shape the client reads. Neither the shared route list nor the OpenAPI document is edited for this client.
/// </summary>
public sealed class ArtifactClientRouteTests : IDisposable
{
    private readonly DemoTestHost _host = new();

    /// <summary>Calls every artifact route; each recorded call is a documented operation.</summary>
    [Fact]
    public async Task EveryRouteTheArtifactClientCallsIsDocumented()
    {
        var ct = TestContext.Current.CancellationToken;
        using var api = new ApiHttp(_host.Http);
        var client = new ArtifactWebClient(api, _host.Time);

        await client.ListAsync(ct);
        await client.GetAsync(DemoArtifactFixtures.NotesId, ct);
        await client.GetContentAsync(DemoArtifactFixtures.NotesId, 1, ct);
        await client.GetContentAsync(DemoArtifactFixtures.MetadataOnlyId, 1, ct);
        await client.RenderUrlAsync(DemoArtifactFixtures.NotesId, 1, ct);

        var used = _host.Recorder.Requests.Select(Shape).Distinct(StringComparer.Ordinal).ToList();
        used.Count.ShouldBe(4, "the list, one header, the content of a version and a render ticket");
        var documented = await ReadDocumentedAsync(ct).ConfigureAwait(true);
        var missing = used.Where(route => !documented.Contains(route)).ToList();

        missing.ShouldBeEmpty("the artifact client calls operations that docs/api/openapi.json does not document: " + string.Join(", ", missing));
    }

    /// <summary>The Demo answers the read routes with their contract shapes, and has no render route, so a ticket is refused.</summary>
    [Fact]
    public async Task TheDemoAnswersEachReadAndRefusesATicket()
    {
        var ct = TestContext.Current.CancellationToken;
        using var api = new ApiHttp(_host.Http);
        var client = new ArtifactWebClient(api, _host.Time);

        var list = await client.ListAsync(ct);
        list.State.ShouldBe(ArtifactCallState.Ok);
        list.Value!.Artifacts.Count.ShouldBe(3);

        (await client.GetContentAsync(DemoArtifactFixtures.NotesId, 1, ct)).State.ShouldBe(ArtifactCallState.Ok);
        (await client.GetContentAsync(DemoArtifactFixtures.MetadataOnlyId, 1, ct)).State.ShouldBe(ArtifactCallState.SyncOff);
        (await client.RenderUrlAsync(DemoArtifactFixtures.NotesId, 1, ct)).State.ShouldBe(ArtifactCallState.NotFound);
    }

    /// <summary>Disposes the Demo host.</summary>
    public void Dispose() => _host.Dispose();

    /// <summary>The method and the route with every variable segment replaced by <c>{}</c>, as the OpenAPI keys are read.</summary>
    private static string Shape(string recorded)
    {
        var space = recorded.IndexOf(' ', StringComparison.Ordinal);
        var segments = recorded[(space + 1)..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 1; index < segments.Length; index++)
        {
            if (segments[index - 1] is "artifacts" or "versions" or "artifact-render")
            {
                segments[index] = "{}";
            }
        }

        return recorded[..space] + " /" + string.Join('/', segments);
    }

    private static async Task<HashSet<string>> ReadDocumentedAsync(CancellationToken cancellationToken)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json")))
        {
            root = root.Parent;
        }

        root.ShouldNotBeNull("Could not locate the repository root from the test output directory.");
        var text = await File.ReadAllTextAsync(Path.Combine(root.FullName, "docs", "api", "openapi.json"), cancellationToken).ConfigureAwait(true);
        using var document = JsonDocument.Parse(text);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            var shape = "/" + string.Join('/', path.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(static segment => segment.StartsWith('{') ? "{}" : segment));
            foreach (var operation in path.Value.EnumerateObject().Where(static member => member.Name is "get" or "put" or "post" or "delete"))
            {
                operations.Add(operation.Name.ToUpperInvariant() + " " + shape);
            }
        }

        return operations;
    }
}
