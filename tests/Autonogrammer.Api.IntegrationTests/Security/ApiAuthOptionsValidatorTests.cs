using Autonogrammer.Api.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>The <see cref="ApiAuthOptionsValidator"/> rules per mode and environment. Every failure names its configuration key and none echoes a secret.</summary>
public sealed class ApiAuthOptionsValidatorTests
{
    private static readonly string ValidDigest = new('a', 64);

    /// <summary>Verifies owner-token mode with one valid digest passes in Production.</summary>
    [Fact]
    public void OwnerToken_WithValidDigest_Passes() =>
        Validate(OwnerOptions(), Environments.Production).Succeeded.ShouldBeTrue();

    /// <summary>Verifies owner-token mode without a digest fails naming the key.</summary>
    [Fact]
    public void OwnerToken_WithoutDigest_FailsNamingKey()
    {
        var options = OwnerOptions();
        options.OwnerToken.Sha256.Clear();

        Failures(options).ShouldContain(static message => message.Contains("Api:Auth:OwnerToken:Sha256", StringComparison.Ordinal));
    }

    /// <summary>Verifies four digests fail and three pass.</summary>
    [Fact]
    public void OwnerToken_DigestCount_IsBoundedToThree()
    {
        var options = OwnerOptions();
        options.OwnerToken.Sha256 = [ValidDigest, new('b', 64), new('c', 64)];
        Validate(options, Environments.Production).Succeeded.ShouldBeTrue();

        options.OwnerToken.Sha256.Add(new('d', 64));
        Failures(options).ShouldContain(static message => message.Contains("Api:Auth:OwnerToken:Sha256", StringComparison.Ordinal));
    }

    /// <summary>Verifies an uppercase, short or non-hex digest fails without echoing the value.</summary>
    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("abc")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void OwnerToken_MalformedDigest_FailsWithoutEchoingIt(string digest)
    {
        var options = OwnerOptions();
        options.OwnerToken.Sha256 = [digest];

        var failures = Failures(options);

        failures.ShouldContain(static message => message.Contains("Api:Auth:OwnerToken:Sha256", StringComparison.Ordinal));
        failures.ShouldAllBe(message => !message.Contains(digest, StringComparison.Ordinal));
    }

    /// <summary>Verifies owner-token mode refuses an authority or audience.</summary>
    [Fact]
    public void OwnerToken_WithAuthorityOrAudience_Fails()
    {
        var options = OwnerOptions();
        options.Authority = "https://issuer.test.invalid/";
        options.Audience = "api";

        Failures(options).ShouldContain(static message => message.Contains("Api:Auth:Authority", StringComparison.Ordinal));
    }

    /// <summary>Verifies an empty subject and an out-of-range failure budget fail naming their keys.</summary>
    [Fact]
    public void OwnerToken_EmptySubjectAndBadBudget_Fail()
    {
        var options = OwnerOptions();
        options.OwnerToken.Subject = " ";
        options.OwnerToken.MaxFailuresPerMinute = 0;

        var failures = Failures(options);

        failures.ShouldContain(static message => message.Contains("Api:Auth:OwnerToken:Subject", StringComparison.Ordinal));
        failures.ShouldContain(static message => message.Contains("Api:Auth:OwnerToken:MaxFailuresPerMinute", StringComparison.Ordinal));
    }

    /// <summary>Verifies an OIDC deployment outside Development needs an allow-list or the explicit any-user flag.</summary>
    [Fact]
    public void Oidc_Production_WithoutAllowList_FailsNamingKeys()
    {
        var options = OidcOptions();

        Failures(options).ShouldContain(static message =>
            message.Contains("Api:Auth:AllowedSubjects", StringComparison.Ordinal) && message.Contains("Api:Auth:AllowAnyAuthenticatedUser", StringComparison.Ordinal));
    }

    /// <summary>Verifies an allow-list, or the any-user flag, satisfies the OIDC rule.</summary>
    [Fact]
    public void Oidc_Production_WithAllowListOrAnyUser_Passes()
    {
        var listed = OidcOptions();
        listed.AllowedSubjects = ["alice"];
        var any = OidcOptions();
        any.AllowAnyAuthenticatedUser = true;

        Validate(listed, Environments.Production).Succeeded.ShouldBeTrue();
        Validate(any, Environments.Production).Succeeded.ShouldBeTrue();
    }

    /// <summary>Verifies Development does not require an allow-list or an authority.</summary>
    [Fact]
    public void Oidc_Development_NeedsNothing() =>
        Validate(new ApiAuthOptions(), Environments.Development).Succeeded.ShouldBeTrue();

    /// <summary>Verifies the OIDC authority rules are unchanged.</summary>
    [Fact]
    public void Oidc_Production_KeepsAuthorityRules()
    {
        var options = new ApiAuthOptions { AllowAnyAuthenticatedUser = true };

        var failures = Failures(options);

        failures.ShouldContain("Api:Auth:Authority is required outside the Development environment.");
        failures.ShouldContain("Api:Auth:Audience is required outside the Development environment.");
    }

    /// <summary>Verifies the development principal stays Development-only in either mode.</summary>
    [Fact]
    public void DevelopmentPrincipal_OutsideDevelopment_FailsInBothModes()
    {
        var owner = OwnerOptions();
        owner.AllowDevelopmentPrincipal = true;
        var oidc = OidcOptions();
        oidc.AllowAnyAuthenticatedUser = true;
        oidc.AllowDevelopmentPrincipal = true;

        Failures(owner).ShouldContain("Api:Auth:AllowDevelopmentPrincipal is allowed only in the Development environment.");
        Failures(oidc).ShouldContain("Api:Auth:AllowDevelopmentPrincipal is allowed only in the Development environment.");
    }

    private static IReadOnlyList<string> Failures(ApiAuthOptions options)
    {
        var result = Validate(options, Environments.Production);
        result.Failed.ShouldBeTrue();
        return result.Failures!.ToArray();
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(ApiAuthOptions options, string environment) =>
        new ApiAuthOptionsValidator(new TestEnvironment(environment)).Validate(null, options);

    private static ApiAuthOptions OwnerOptions() => new()
    {
        Mode = ApiAuthMode.OwnerToken,
        OwnerToken = new ApiOwnerTokenOptions { Sha256 = [ValidDigest] },
    };

    private static ApiAuthOptions OidcOptions() => new()
    {
        Authority = "https://issuer.test.invalid/",
        Audience = "api",
    };

    private sealed class TestEnvironment : IHostEnvironment
    {
        public TestEnvironment(string name)
        {
            EnvironmentName = name;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Autonogrammer.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
