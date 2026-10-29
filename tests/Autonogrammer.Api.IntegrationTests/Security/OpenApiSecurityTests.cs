using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>
/// The security metadata of the served OpenAPI document: the <c>bearerAuth</c> and <c>DispatchDevice</c> schemes, the document-level
/// requirement, an empty <c>security</c> list on exactly the anonymous operations, and the 401 and 403 answers of every other operation.
/// </summary>
public sealed class OpenApiSecurityTests
{
    private static readonly string[] HttpMethods = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

    /// <summary>
    /// The operations that carry anonymous endpoint metadata. A new anonymous operation (the artifact-render ticket route of phase 71, the
    /// dispatch device connect route of phase 74) must be added here deliberately.
    /// </summary>
    private static readonly string[] ExpectedAnonymous = ["get /auth/config", "get /source", "post /hooks/automations/{id}"];

    /// <summary>Verifies both schemes are declared and the document requires bearerAuth.</summary>
    [Fact]
    public async Task Document_DeclaresBothSchemes_AndRequiresBearer()
    {
        using var document = await ServedAsync().ConfigureAwait(true);
        var root = document.RootElement;

        var schemes = root.GetProperty("components").GetProperty("securitySchemes");
        var bearer = schemes.GetProperty("bearerAuth");
        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
        bearer.GetProperty("bearerFormat").GetString().ShouldBe("JWT");
        schemes.TryGetProperty("DispatchDevice", out _).ShouldBeTrue();

        var requirement = root.GetProperty("security").EnumerateArray().ShouldHaveSingleItem();
        requirement.TryGetProperty("bearerAuth", out _).ShouldBeTrue();
    }

    /// <summary>Verifies security is empty on exactly the anonymous operations.</summary>
    [Fact]
    public async Task Security_IsEmpty_OnExactlyTheAnonymousOperations()
    {
        using var document = await ServedAsync().ConfigureAwait(true);

        var emptySecurity = Operations(document)
            .Where(static op => op.Value.TryGetProperty("security", out var security) && security.ValueKind == JsonValueKind.Array && security.GetArrayLength() == 0)
            .Select(static op => op.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        emptySecurity.ShouldBe(ExpectedAnonymous.Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Verifies a protected route answers 401 and 403 in the document and the anonymous operations gain neither from the boundary. The webhook
    /// receiver documents its own 401 (a bad signature), which is not the bearer boundary.
    /// </summary>
    [Fact]
    public async Task ProtectedOperations_Declare401And403_AnonymousOnesDoNot()
    {
        using var document = await ServedAsync().ConfigureAwait(true);
        var operations = Operations(document).ToDictionary(static op => op.Key, static op => op.Value, StringComparer.Ordinal);

        var sessions = operations["get /sessions"].GetProperty("responses");
        sessions.TryGetProperty("401", out _).ShouldBeTrue();
        sessions.TryGetProperty("403", out _).ShouldBeTrue();
        foreach (var anonymous in ExpectedAnonymous)
        {
            var responses = operations[anonymous].GetProperty("responses");
            if (!anonymous.StartsWith("post /hooks/", StringComparison.Ordinal))
            {
                responses.TryGetProperty("401", out _).ShouldBeFalse(anonymous);
            }

            responses.TryGetProperty("403", out _).ShouldBeFalse(anonymous);
        }
    }

    /// <summary>Verifies every operation with security omitted is protected by the document-level bearer requirement and none is silently open.</summary>
    [Fact]
    public async Task EveryOtherOperation_InheritsTheDocumentRequirement()
    {
        using var document = await ServedAsync().ConfigureAwait(true);

        var others = Operations(document).Where(static op => !ExpectedAnonymous.Contains(op.Key, StringComparer.Ordinal)).ToArray();

        others.ShouldNotBeEmpty();
        foreach (var (key, operation) in others)
        {
            operation.TryGetProperty("security", out _).ShouldBeFalse($"{key} must inherit the document-level requirement");
        }
    }

    private static IEnumerable<KeyValuePair<string, JsonElement>> Operations(JsonDocument document)
    {
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject().Where(static member => HttpMethods.Contains(member.Name, StringComparer.Ordinal)))
            {
                yield return new KeyValuePair<string, JsonElement>($"{operation.Name} {path.Name}", operation.Value);
            }
        }
    }

    private static async Task<JsonDocument> ServedAsync()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var text = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), testToken).ConfigureAwait(false);
        return JsonDocument.Parse(text);
    }
}
