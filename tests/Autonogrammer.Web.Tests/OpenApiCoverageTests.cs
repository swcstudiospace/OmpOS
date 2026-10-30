using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The routes the web client calls exist in <c>docs/api/openapi.json</c>, which the API build regenerates. The Demo handler and the API stay contract-identical because both are checked against the same document.</summary>
public sealed class OpenApiCoverageTests : IDisposable
{
    private readonly DemoTestHost _host = new();

    /// <summary>Every operation the client uses (except the health probe, which the API maps outside OpenAPI) is a documented operation.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task EveryOperationTheClientUsesIsInTheOpenApiDocument()
    {
        var ct = TestContext.Current.CancellationToken;
        var documented = await ReadDocumentedOperationsAsync(ct).ConfigureAwait(true);
        var used = await ClientRouteSurface.ExerciseAsync(_host, ct).ConfigureAwait(true);

        var missing = used
            .Where(static route => !route.EndsWith("/health/ready", StringComparison.Ordinal))
            .Where(route => !documented.Contains(route))
            .ToList();

        missing.ShouldBeEmpty("the client calls operations that docs/api/openapi.json does not document (the API build regenerates the file): " + string.Join(", ", missing));
    }

    private static async Task<HashSet<string>> ReadDocumentedOperationsAsync(CancellationToken cancellationToken)
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
            var template = "/" + string.Join('/', path.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(static segment => segment.StartsWith('{') ? "{}" : segment));
            foreach (var operation in path.Value.EnumerateObject())
            {
                _ = operations.Add(operation.Name.ToUpperInvariant() + " " + template);
            }
        }

        return operations;
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
