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

        failures.ShouldContain("Api:Auth:Authority is required outside the Development environment.", StringComparer.Ordinal);
        failures.ShouldContain("Api:Auth:Audience is required outside the Development environment.", StringComparer.Ordinal);
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

        Failures(owner).ShouldContain("Api:Auth:AllowDevelopmentPrincipal is allowed only in the Development environment.", StringComparer.Ordinal);
        Failures(oidc).ShouldContain("Api:Auth:AllowDevelopmentPrincipal is allowed only in the Development environment.", StringComparer.Ordinal);
    }

    /// <summary>Verifies a missing or non-https ValidIssuer fails in Production naming the key, and Development needs none.</summary>
    [Fact]
    public void Oidc_ValidIssuer_RequiredAndHttpsOutsideDevelopment()
    {
        var missing = OidcOptions();
        missing.AllowAnyAuthenticatedUser = true;
        missing.ValidIssuer = null;
        var insecure = OidcOptions();
        insecure.AllowAnyAuthenticatedUser = true;
        insecure.ValidIssuer = "http://issuer.test.invalid/";

        Failures(missing).ShouldContain("Api:Auth:ValidIssuer is required outside the Development environment.", StringComparer.Ordinal);
        Failures(insecure).ShouldContain("Api:Auth:ValidIssuer must be an absolute https URI.", StringComparer.Ordinal);
        Validate(new ApiAuthOptions(), Environments.Development).Succeeded.ShouldBeTrue();
    }

    /// <summary>Verifies Algorithms accepts ES256 and RS256 only.</summary>
    [Fact]
    public void Oidc_Algorithms_AreLimitedToEs256AndRs256()
    {
        var good = ValidOidc();
        good.Algorithms = ["ES256", "RS256"];
        var bad = ValidOidc();
        bad.Algorithms = ["ES256", "HS256"];

        Validate(good, Environments.Production).Succeeded.ShouldBeTrue();
        Failures(bad).ShouldContain("Api:Auth:Algorithms may list only ES256 and RS256.", StringComparer.Ordinal);
        new ApiAuthOptions().EffectiveAlgorithms.ShouldBe(["ES256"]);
    }

    /// <summary>Verifies a valid Client block and an absent Client block pass.</summary>
    [Fact]
    public void Client_ValidBlockAndAbsentBlock_Pass()
    {
        var valid = ValidOidc();
        valid.Client = new ApiAuthClientOptions { ProjectUrl = "https://project.supabase.co", PublishableKey = "sb_publishable_abc", MagicLinkEnabled = true };

        Validate(valid, Environments.Production).Succeeded.ShouldBeTrue();
        Validate(ValidOidc(), Environments.Production).Succeeded.ShouldBeTrue();
    }

    /// <summary>Verifies a secret key as the publishable key fails naming the key and never echoing the value, however it is padded or cased.</summary>
    [Theory]
    [InlineData("sb_secret_do-not-echo")]
    [InlineData(" sb_secret_do-not-echo")]
    [InlineData("\tsb_secret_do-not-echo\n")]
    [InlineData("SB_SECRET_do-not-echo")]
    public void Client_SecretKey_FailsNamingKeyWithoutEchoingIt(string key)
    {
        var options = ValidOidc();
        options.Client = new ApiAuthClientOptions { ProjectUrl = "https://project.supabase.co", PublishableKey = key };

        var failures = Failures(options);

        failures.ShouldContain(static m => m.Contains("Api:Auth:Client:PublishableKey", StringComparison.Ordinal));
        failures.ShouldNotContain(static m => m.Contains("do-not-echo", StringComparison.Ordinal));
    }

    /// <summary>Verifies a service-role JWT with surrounding whitespace still fails.</summary>
    [Fact]
    public void Client_ServiceRoleJwtWithSurroundingWhitespace_Fails()
    {
        var options = ValidOidc();
        options.Client = new ApiAuthClientOptions { ProjectUrl = "https://project.supabase.co", PublishableKey = " " + Jwt("service_role") + "\n" };

        Failures(options).ShouldContain(static m => m.Contains("Api:Auth:Client:PublishableKey", StringComparison.Ordinal));
    }

    /// <summary>Verifies a JWT whose role claim is service_role fails, and an anon-role JWT passes.</summary>
    [Fact]
    public void Client_ServiceRoleJwt_Fails_AnonRoleJwt_Passes()
    {
        var service = ValidOidc();
        service.Client = new ApiAuthClientOptions { ProjectUrl = "https://project.supabase.co", PublishableKey = Jwt("service_role") };
        var anon = ValidOidc();
        anon.Client = new ApiAuthClientOptions { ProjectUrl = "https://project.supabase.co", PublishableKey = Jwt("anon") };

        var failures = Failures(service);

        failures.ShouldContain(static m => m.Contains("Api:Auth:Client:PublishableKey", StringComparison.Ordinal));
        failures.ShouldNotContain(static m => m.Contains("eyJ", StringComparison.Ordinal));
        Validate(anon, Environments.Production).Succeeded.ShouldBeTrue();
    }

    /// <summary>Verifies a non-https or userinfo-bearing project URL fails naming the key.</summary>
    [Theory]
    [InlineData("http://project.supabase.co")]
    [InlineData("https://user:pass@project.supabase.co")]
    [InlineData("not a url")]
    public void Client_BadProjectUrl_FailsNamingKeyWithoutEchoingIt(string candidate)
    {
        var options = ValidOidc();
        options.Client = new ApiAuthClientOptions { ProjectUrl = candidate, PublishableKey = "sb_publishable_abc" };

        var failures = Failures(options);

        failures.ShouldContain(static m => m.Contains("Api:Auth:Client:ProjectUrl", StringComparison.Ordinal));
        failures.ShouldNotContain(static m => m.Contains("pass@", StringComparison.Ordinal));
    }

    /// <summary>Verifies the link method without the code method fails naming both keys.</summary>
    [Fact]
    public void Client_MagicLinkWithoutEmailCode_Fails()
    {
        var options = ValidOidc();
        options.Client = new ApiAuthClientOptions { MagicLinkEnabled = true, EmailCodeEnabled = false };

        Failures(options).ShouldContain(static m =>
            m.Contains("Api:Auth:Client:MagicLinkEnabled", StringComparison.Ordinal) && m.Contains("Api:Auth:Client:EmailCodeEnabled", StringComparison.Ordinal));
    }

    /// <summary>Verifies any Client member, and Algorithms, fail in owner-token mode while an unset block passes.</summary>
    [Fact]
    public void OwnerToken_ClientOrAlgorithms_Fail()
    {
        var client = OwnerOptions();
        client.Client = new ApiAuthClientOptions { ProjectUrl = "https://project.supabase.co" };
        var flag = OwnerOptions();
        flag.Client = new ApiAuthClientOptions { MagicLinkEnabled = true };
        var algorithms = OwnerOptions();
        algorithms.Algorithms = ["ES256"];

        Failures(client).ShouldContain(static m => m.Contains("Api:Auth:Client", StringComparison.Ordinal));
        Failures(flag).ShouldContain(static m => m.Contains("Api:Auth:Client", StringComparison.Ordinal));
        Failures(algorithms).ShouldContain(static m => m.Contains("Api:Auth:Algorithms", StringComparison.Ordinal));
        Validate(OwnerOptions(), Environments.Production).Succeeded.ShouldBeTrue();
    }

    private static ApiAuthOptions ValidOidc()
    {
        var options = OidcOptions();
        options.AllowAnyAuthenticatedUser = true;
        return options;
    }

    private static string Jwt(string role)
    {
        static string B64(string json) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{B64($"{{\"role\":\"{role}\"}}")}.c2ln";
    }

    private static string[] Failures(ApiAuthOptions options)
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
        ValidIssuer = "https://issuer.test.invalid/",
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
