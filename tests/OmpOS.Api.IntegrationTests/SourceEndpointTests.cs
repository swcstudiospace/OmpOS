using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using OmpOS.Api.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// <c>GET /source</c>, the AGPL-3.0 section 13 source offer, is anonymous like <c>/health/live</c> and answers exactly the documented members; the
/// revision comes from the build metadata or <c>GIT_SHA</c> and is otherwise <c>unknown</c>.
/// </summary>
public sealed class SourceEndpointTests
{
    private static readonly Regex HexRevision = new("^[0-9a-f]{7,64}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>A caller with no token gets the offer on a host that validates bearer tokens, and the body has exactly the documented members.</summary>
    [Fact]
    public async Task Source_IsAnonymous_AndHasExactlyTheDocumentedMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/source", UriKind.Relative), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        body.RootElement.EnumerateObject().Select(static property => property.Name).Order(StringComparer.Ordinal)
            .ShouldBe(["licence", "licenceUrl", "revision", "sourceUrl", "version"]);
        body.RootElement.GetProperty("licence").GetString().ShouldBe("AGPL-3.0-or-later");
        body.RootElement.GetProperty("licenceUrl").GetString().ShouldBe("https://www.gnu.org/licenses/agpl-3.0.html");
        body.RootElement.GetProperty("sourceUrl").GetString().ShouldBe("https://github.com/swcstudiospace/OmpOS");
        var revision = body.RootElement.GetProperty("revision").GetString().ShouldNotBeNull();
        (string.Equals(revision, "unknown", StringComparison.Ordinal) || HexRevision.IsMatch(revision)).ShouldBeTrue($"revision '{revision}' is a git object name or unknown");
        body.RootElement.GetProperty("version").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>The revision is the build metadata of the informational version, else GIT_SHA, else unknown; only a hexadecimal object name is accepted.</summary>
    [Theory]
    [InlineData("0.15.0+0123abcdef", null, "0123abcdef")]
    [InlineData("0.15.0+0123ABCDEF.dirty", null, "0123abcdef")]
    [InlineData("0.15.0", "DEADBEEFCAFE", "deadbeefcafe")]
    [InlineData("0.15.0+0123abcdef", "deadbeefcafe", "0123abcdef")]
    [InlineData("0.15.0+not-a-sha", null, "unknown")]
    [InlineData("0.15.0", "not a sha", "unknown")]
    [InlineData("0.15.0", "abc12", "unknown")]
    [InlineData(null, null, "unknown")]
    public void RevisionOf_PicksABuildRevisionOrUnknown(string? informational, string? environment, string expected)
    {
        SourceInfoProvider.RevisionOf(informational, environment).ShouldBe(expected);
    }

    /// <summary>The offer is read on every host with the same body, so a second read matches the first.</summary>
    [Fact]
    public async Task Source_IsStable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();

        var first = await client.GetStringAsync(new Uri("/source", UriKind.Relative), ct).ConfigureAwait(true);
        var second = await client.GetStringAsync(new Uri("/source", UriKind.Relative), ct).ConfigureAwait(true);

        second.ShouldBe(first);
        using var document = JsonDocument.Parse(first);
        document.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
    }
}
