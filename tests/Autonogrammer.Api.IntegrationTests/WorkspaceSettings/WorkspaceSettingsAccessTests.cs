using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary>Who may read the settings and workspace endpoints: authenticated callers only, and when <c>Api:Workspace:Owners</c> is set only the callers it names.</summary>
public sealed class WorkspaceSettingsAccessTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, string?> OwnedBy(string subject) => new(StringComparer.Ordinal) { ["Api:Workspace:Owners:0"] = subject };

    /// <summary>With no bearer token every route answers 401.</summary>
    [Theory]
    [MemberData(nameof(WorkspaceRoutes.All), MemberType = typeof(WorkspaceRoutes))]
    public async Task WithoutAToken_IsUnauthorized(string route)
    {
        using var workspace = new TempWorkspace();
        using var factory = WorkspaceHost.Jwt(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, route, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>With the default owner list of the host helper, a listed authenticated caller reads every route.</summary>
    [Theory]
    [MemberData(nameof(WorkspaceRoutes.All), MemberType = typeof(WorkspaceRoutes))]
    public async Task WithTheDefaultOwnerList_AListedCaller_Reads(string route)
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write("a.txt", "a");
        using var factory = WorkspaceHost.Jwt(workspace);
        using var client = WebAppTestHost.AsPrincipal(factory, "bob");

        using var response = await WorkspaceHost.GetAsync(client, route, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>With an empty owner list and no developer principal every authenticated caller gets 404 on every route.</summary>
    [Theory]
    [MemberData(nameof(WorkspaceRoutes.All), MemberType = typeof(WorkspaceRoutes))]
    public async Task WithAnEmptyOwnerList_EveryAuthenticatedCaller_Gets404(string route)
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write("a.txt", "secret-text");
        using var factory = WorkspaceHost.Jwt(workspace, owners: []);
        using var client = WebAppTestHost.AsPrincipal(factory, "bob");

        using var response = await WorkspaceHost.GetAsync(client, route, Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "workspace_not_found", Token).ConfigureAwait(true);
    }

    /// <summary>With an empty owner list the Development developer principal still reads.</summary>
    [Fact]
    public async Task WithAnEmptyOwnerList_TheDevelopmentPrincipal_Reads()
    {
        using var factory = WorkspaceHost.Dev(null);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/settings/runtime", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>With an empty owner list the single operator of an owner-token deployment still reads.</summary>
    [Fact]
    public async Task WithAnEmptyOwnerList_TheOwnerTokenOperator_Reads()
    {
        using var factory = Security.OwnerTokenTestHost.CreateFactory();
        using var client = factory.CreateClient();
        Security.OwnerTokenTestHost.UseBearer(client, Security.OwnerTokenTestHost.Token);

        using var response = await WorkspaceHost.GetAsync(client, "/settings/runtime", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>A caller the owner list names reads every route.</summary>
    [Theory]
    [MemberData(nameof(WorkspaceRoutes.All), MemberType = typeof(WorkspaceRoutes))]
    public async Task AListedOwner_Reads(string route)
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write("a.txt", "a");
        using var factory = WorkspaceHost.Jwt(workspace, OwnedBy("alice"));
        using var client = WebAppTestHost.AsPrincipal(factory, "alice");

        using var response = await WorkspaceHost.GetAsync(client, route, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>A caller outside the owner list gets 404 on every route and learns nothing about the workspace.</summary>
    [Theory]
    [MemberData(nameof(WorkspaceRoutes.All), MemberType = typeof(WorkspaceRoutes))]
    public async Task AnotherCaller_Gets404(string route)
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write("a.txt", "secret-text");
        using var factory = WorkspaceHost.Jwt(workspace, OwnedBy("alice"));
        using var client = WebAppTestHost.AsPrincipal(factory, "bob");

        using var response = await WorkspaceHost.GetAsync(client, route, Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "workspace_not_found", Token).ConfigureAwait(true);
    }

    /// <summary>The development principal is checked against the owner list like any other caller.</summary>
    [Fact]
    public async Task TheDevelopmentPrincipal_IsCheckedAgainstTheOwnerList()
    {
        using var factory = WorkspaceHost.Dev(null, OwnedBy("someone-else"));
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/settings/runtime", Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "workspace_not_found", Token).ConfigureAwait(true);
    }
}
