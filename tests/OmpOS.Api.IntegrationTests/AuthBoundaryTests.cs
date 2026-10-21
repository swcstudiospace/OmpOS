using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Security;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// OPS-04 proof: the ambient <see cref="IUserContext"/> is the single dev principal with the
/// reserved owner id, created sessions carry that owner, and requests pass the auth boundary.
/// </summary>
public sealed class AuthBoundaryTests
{
    /// <summary>Verifies the test host resolves the single dev principal with the reserved owner id.</summary>
    [Fact]
    public void UserContext_ResolvesSingleDevPrincipal()
    {
        using var factory = new OmpApiFactory();
        using var scope = factory.Services.CreateScope();

        var user = scope.ServiceProvider.GetRequiredService<IUserContext>();

        user.ShouldBeOfType<SingleDevPrincipal>();
        user.Name.ShouldBe(SingleDevPrincipal.DevName);
    }

    /// <summary>Verifies a created session carries the dev principal as its owner.</summary>
    [Fact]
    public async Task CreatedSession_CarriesDevPrincipalOwnerId()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var expectedOwner = scope.ServiceProvider.GetRequiredService<IUserContext>().Name;

        var id = await SessionClient.CreateSessionAsync(client, testToken);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken);

        session.OwnerId.ShouldBe(expectedOwner);
        session.OwnerId.ShouldBe(SingleDevPrincipal.DevName);
    }

    /// <summary>Verifies requests pass the auth boundary with the dev principal instead of a 401.</summary>
    [Fact]
    public async Task Requests_PassAuthBoundaryWithoutUnauthorized()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var list = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        list.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var created = await client.PostAsync(new Uri("/sessions", UriKind.Relative), content, testToken);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }
}
