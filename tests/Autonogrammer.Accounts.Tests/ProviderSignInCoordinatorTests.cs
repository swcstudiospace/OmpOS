using System.Web;
using Autonogrammer.Accounts.Tests.Support;
using Autonogrammer.Contracts.Accounts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Tests;

/// <summary>
/// The provider sign-in of the coordinator: the address it opens carries the provider, the return address and an S256 challenge, and never the
/// verifier; and the refusals that need no request.
/// </summary>
public sealed class ProviderSignInCoordinatorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BeginProviderOpensAnAuthorizeAddressWithAnS256ChallengeAndNoVerifier()
    {
        using AccountsHarness harness = AccountsHarness.Create();

        AuthResult<Uri> begun = await harness.Coordinator.BeginProviderAsync("google", _ => new Uri("https://app.example.org/"), Token);

        begun.IsSuccess.ShouldBeTrue();
        var query = HttpUtility.ParseQueryString(begun.Value.Query);
        query["provider"].ShouldBe("google");
        query["redirect_to"].ShouldBe("https://app.example.org/");
        query["code_challenge_method"].ShouldBe("s256");
        query["code_challenge"].ShouldNotBeNullOrWhiteSpace();
        begun.Value.AbsoluteUri.ShouldNotContain("code_verifier", Case.Sensitive);
    }

    [Fact]
    public async Task BeginProviderRefusesAProviderTheClientDoesNotStartWithoutAnyRequest()
    {
        using AccountsHarness harness = AccountsHarness.Create();

        AuthResult<Uri> begun = await harness.Coordinator.BeginProviderAsync("twitter", _ => new Uri("https://app.example.org/"), Token);

        begun.IsSuccess.ShouldBeFalse();
        harness.Handler.Requests.Count.ShouldBe(0);
    }

    [Fact]
    public async Task BeginProviderNeedsAReturnAddress()
    {
        using AccountsHarness harness = AccountsHarness.Create();

        AuthResult<Uri> begun = await harness.Coordinator.BeginProviderAsync("google", _ => null, Token);

        begun.IsSuccess.ShouldBeFalse();
        begun.Failure.Kind.ShouldBe(AuthErrorKind.ConfigUnavailable);
    }
}
