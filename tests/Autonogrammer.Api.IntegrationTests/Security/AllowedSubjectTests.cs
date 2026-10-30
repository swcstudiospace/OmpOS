using System.Net;
using System.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>The OIDC allow-list: an authenticated subject that is not listed gets 403 ProblemDetails, a listed one 200.</summary>
public sealed class AllowedSubjectTests
{
    /// <summary>Verifies a listed subject is served and an unlisted subject receives 403 with a problem body.</summary>
    [Fact]
    public async Task UnlistedSubject_Gets403Problem_ListedSubjectGets200()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Api:Auth:AllowedSubjects:0"] = "alice";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        using var listed = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("mallory"));
        using var unlisted = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);
        var body = await unlisted.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);

        listed.StatusCode.ShouldBe(HttpStatusCode.OK);
        unlisted.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        body.ShouldContain("subject_not_allowed");
        body.ShouldNotContain("mallory");
    }

    /// <summary>Verifies the match is exact and case-sensitive.</summary>
    [Fact]
    public async Task SubjectMatch_IsExactAndCaseSensitive()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Api:Auth:AllowedSubjects:0"] = "alice";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("Alice"));

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>Verifies a Production OIDC host without an allow-list refuses to start.</summary>
    [Fact]
    public void Production_WithoutAllowList_FailsStartup()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Api:Auth:AllowAnyAuthenticatedUser"] = "false";
        using var factory = new ApiFactory(config, environment: "Production");

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Api:Auth:AllowedSubjects");
    }
}
