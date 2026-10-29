using System.Text.Json;
using Autonogrammer.Contracts;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Contracts.Agents;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Tests;

/// <summary>
/// Verifies that the Accounts contracts round trip through the source-generated JSON context, that the existing three-member auth configuration answer is
/// unchanged on the wire, that the stored session shape is strict, and that the contract version was not touched.
/// </summary>
public sealed class AccountsContractsJsonTests
{
    [Fact]
    public void AnOwnerTokenAnswerKeepsItsThreeMembersWithJsonNullsExactlyAsToday()
    {
        string json = JsonSerializer.Serialize(new AuthConfigResponse("owner-token", null, null), AccountsJsonContext.Default.AuthConfigResponse);

        json.ShouldBe("""{"mode":"owner-token","authority":null,"audience":null}""");
    }

    [Fact]
    public void TheExistingThreeMemberAnswersStillDeserialize()
    {
        AuthConfigResponse? oidc = JsonSerializer.Deserialize(
            """{"mode":"oidc","authority":"https://issuer.example/auth/v1","audience":"authenticated"}""",
            AccountsJsonContext.Default.AuthConfigResponse);
        AuthConfigResponse? development = JsonSerializer.Deserialize("""{"mode":"development","authority":null,"audience":null}""", AccountsJsonContext.Default.AuthConfigResponse);

        oidc.ShouldNotBeNull();
        oidc.Authority.ShouldBe("https://issuer.example/auth/v1");
        oidc.ProjectUrl.ShouldBeNull();
        oidc.Methods.ShouldBeNull();
        development.ShouldNotBeNull();
        development.Mode.ShouldBe("development");
    }

    [Fact]
    public void TheClientBlockRoundTripsInCamelCaseAndOmitsNothingItCarries()
    {
        var config = new AuthConfigResponse(
            "oidc",
            "https://example.supabase.co/auth/v1",
            "authenticated",
            new Uri("https://example.supabase.co"),
            "sb_publishable_x",
            new AuthMethods(EmailCode: true, MagicLink: false),
            new AuthEmailCode(ExpirySeconds: 3600, ResendCooldownSeconds: 60),
            SignupEmailHasCode: false);

        string json = JsonSerializer.Serialize(config, AccountsJsonContext.Default.AuthConfigResponse);
        AuthConfigResponse? back = JsonSerializer.Deserialize(json, AccountsJsonContext.Default.AuthConfigResponse);

        json.ShouldContain("\"projectUrl\":\"https://example.supabase.co\"");
        json.ShouldContain("\"publishableKey\":\"sb_publishable_x\"");
        json.ShouldContain("\"methods\":{\"emailCode\":true,\"magicLink\":false}");
        json.ShouldContain("\"emailCode\":{\"expirySeconds\":3600,\"resendCooldownSeconds\":60}");
        json.ShouldContain("\"signupEmailHasCode\":false");
        back.ShouldBe(config);
    }

    [Fact]
    public void AnAccountWithoutARowOmitsItsNullMembers()
    {
        string json = JsonSerializer.Serialize(new AccountResponse("owner", null, null, null, ["cowork"]), AccountsJsonContext.Default.AccountResponse);
        AccountResponse? back = JsonSerializer.Deserialize(json, AccountsJsonContext.Default.AccountResponse);

        json.ShouldBe("""{"id":"owner","features":["cowork"]}""");
        back.ShouldNotBeNull();
        back.Email.ShouldBeNull();
        back.Features.ShouldBe(["cowork"]);
    }

    [Fact]
    public void AnAccountWithARowRoundTripsItsTimes()
    {
        var account = new AccountResponse("user-1", "person@example.org", new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero), []);

        AccountResponse? back = JsonSerializer.Deserialize(JsonSerializer.Serialize(account, AccountsJsonContext.Default.AccountResponse), AccountsJsonContext.Default.AccountResponse);

        back.ShouldNotBeNull();
        back.Id.ShouldBe("user-1");
        back.Email.ShouldBe("person@example.org");
        back.CreatedAt.ShouldBe(account.CreatedAt);
        back.LastSeenAt.ShouldBe(account.LastSeenAt);
    }

    [Fact]
    public void ASessionListRoundTripsWithSurfaceLabelsAndTheExistingSummary()
    {
        var summary = new SessionSummary
        {
            Id = "s-1",
            Title = "Fix the build",
            Lifecycle = SessionLifecycle.Running,
            CreatedAt = new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero),
            LastActivity = new DateTimeOffset(2026, 10, 29, 8, 5, 0, TimeSpan.Zero),
        };
        var list = new AccountSessionList([new AccountSessionItem(summary, "code"), new AccountSessionItem(summary with { Id = "s-2" }, "cowork")]);

        string json = JsonSerializer.Serialize(list, AccountsJsonContext.Default.AccountSessionList);
        AccountSessionList? back = JsonSerializer.Deserialize(json, AccountsJsonContext.Default.AccountSessionList);

        json.ShouldContain("\"surface\":\"code\"");
        json.ShouldContain("\"lifecycle\":\"Running\"");
        back.ShouldNotBeNull();
        back.Items.Select(item => item.Surface).ShouldBe(["code", "cowork"]);
        back.Items[1].Session.Id.ShouldBe("s-2");
    }

    [Fact]
    public void TheEnumsAreWrittenAsStrings()
    {
        JsonSerializer.Serialize(AuthErrorKind.EmailRateLimited, AccountsJsonContext.Default.AuthErrorKind).ShouldBe("\"EmailRateLimited\"");
        JsonSerializer.Serialize(AuthPhase.AwaitingLink, AccountsJsonContext.Default.AuthPhase).ShouldBe("\"AwaitingLink\"");
    }

    [Fact]
    public void TheStoredSessionShapeRejectsUnknownMembersAndRoundTripsItsOwn()
    {
        var stored = new StoredSession(1, "https://p.supabase.co/auth/v1", "refresh", "user-1", null);

        string json = JsonSerializer.Serialize(stored, StoredSessionJsonContext.Default.StoredSession);
        StoredSession? back = JsonSerializer.Deserialize(json, StoredSessionJsonContext.Default.StoredSession);

        json.ShouldBe("""{"schema":1,"issuer":"https://p.supabase.co/auth/v1","refreshToken":"refresh","userId":"user-1"}""");
        back.ShouldBe(stored);
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize(
            """{"schema":1,"issuer":"i","refreshToken":"r","userId":"u","accessToken":"leak"}""",
            StoredSessionJsonContext.Default.StoredSession));
    }

    [Fact]
    public void TheWireContextSkipsMembersTheServiceAddsLater()
    {
        TokenResponse? token = JsonSerializer.Deserialize(
            """{"access_token":"a","token_type":"bearer","expires_in":3600,"refresh_token":"r","user":{"id":"u","email":"e@example.org","new_member":1},"brand_new":{"x":1}}""",
            GoTrueJsonContext.Default.TokenResponse);

        token.ShouldNotBeNull();
        token.AccessToken.ShouldBe("a");
        token.User!.Id.ShouldBe("u");
    }

    [Fact]
    public void TheContractVersionIsTheScaffoldValue()
    {
        ContractVersion.Current.ShouldBe("1.6.0");
    }
}
