using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// GAP-OP-1 guard: every documented operation carries a unique <c>operationId</c>
/// (via <c>WithName</c> in source) and the contract is at <c>1.1.0</c>.
/// The served document equals the committed build-time document (GuardTests), so this
/// committed-file check proves the served document too. Method names are the OpenAPI
/// method keys; parameters and other members are not operations.
/// </summary>
public sealed class OpenApiOperationIdTests
{
    private static readonly string[] HttpMethods = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    /// <summary>Every operation in docs/api/openapi.json has a non-empty operationId.</summary>
    [Fact]
    public async Task EveryOperationHasOperationId()
    {
        var document = await ReadDocumentAsync().ConfigureAwait(true);
        var missing = ListMissing(document);
        missing.ShouldBeEmpty("every operation must carry an operationId (GAP-OP-1): " + string.Join(", ", missing));
    }

    /// <summary>OperationIds are unique across the document.</summary>
    [Fact]
    public async Task OperationIdsAreUnique()
    {
        var document = await ReadDocumentAsync().ConfigureAwait(true);
        var ids = ListOperationIds(document);
        ids.ShouldNotBeEmpty();
        var distinct = ids.Distinct(StringComparer.Ordinal).Count();
        distinct.ShouldBe(ids.Count, "operationId values must be unique: " + string.Join(", ", ids));
    }

    /// <summary>The contract info version is the additive 1.1.0.</summary>
    [Fact]
    public async Task DocumentInfoVersionIs110()
    {
        var document = await ReadDocumentAsync().ConfigureAwait(true);
        document.RootElement.GetProperty("info").GetProperty("version").GetString().ShouldBe("1.1.0");
    }

    private static async Task<JsonDocument> ReadDocumentAsync()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "api", "openapi.json");
        var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        return JsonDocument.Parse(text);
    }

    private static List<string> ListMissing(JsonDocument document)
    {
        var missing = new List<string>();
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!HttpMethods.Contains(operation.Name.ToUpperInvariant()))
                {
                    continue;
                }

                var hasId = operation.Value.TryGetProperty("operationId", out var id)
                    && id.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(id.GetString());
                if (!hasId)
                {
                    missing.Add(operation.Name.ToUpperInvariant() + " " + path.Name);
                }
            }
        }

        return missing;
    }

    private static List<string> ListOperationIds(JsonDocument document)
    {
        var ids = new List<string>();
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!HttpMethods.Contains(operation.Name.ToUpperInvariant()))
                {
                    continue;
                }

                if (operation.Value.TryGetProperty("operationId", out var id) && id.ValueKind == JsonValueKind.String)
                {
                    ids.Add(id.GetString() ?? string.Empty);
                }
            }
        }

        return ids;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not locate Autonogrammer.slnx walking up from " + AppContext.BaseDirectory)
            : directory.FullName;
    }
}
