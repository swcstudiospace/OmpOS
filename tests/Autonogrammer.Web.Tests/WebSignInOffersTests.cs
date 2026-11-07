using Autonogrammer.Contracts.Accounts;
using Autonogrammer.UI.Components.Auth;
using Autonogrammer.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The sign-in options the web head offers: the providers the operator declared, never one the identity service cannot report.</summary>
public sealed class WebSignInOffersTests
{
    /// <summary>A declared provider is offered; a name the web head has no button for is not.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task OnlyTheDeclaredKnownProvidersAreOffered()
    {
        var ct = TestContext.Current.CancellationToken;
        using var account = new WebAccountSession(TimeProvider.System, new ServiceCollection().BuildServiceProvider());
        account.Apply(ConfigWith(["google", "x", "twitter"]), "https://project.example", "https://web.example/");

        (await account.GetEnabledProvidersAsync(ct).ConfigureAwait(true)).ShouldBe(["google", "x"]);
    }

    /// <summary>Nothing is offered before a deployment configuration has been applied.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task NothingIsOfferedBeforeTheConfigurationIsApplied()
    {
        var ct = TestContext.Current.CancellationToken;
        using var account = new WebAccountSession(TimeProvider.System, new ServiceCollection().BuildServiceProvider());

        (await account.GetEnabledProvidersAsync(ct).ConfigureAwait(true)).ShouldBeEmpty();
    }

    private static AuthConfig ConfigWith(string[] providers) =>
        new(
            AuthConfig.OidcMode,
            "https://issuer.example",
            "authenticated",
            new Uri("https://project.example"),
            "sb_publishable_test",
            new AuthMethods(EmailCode: true, MagicLink: false, Providers: providers, Wallet: false));
}
