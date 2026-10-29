using Autonogrammer.Api.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>The <see cref="ApiCorsOptionsValidator"/> accepts only origins a browser can send and the CORS middleware can match.</summary>
public sealed class ApiCorsOptionsValidatorTests
{
    /// <summary>An empty list (no cross-origin traffic) and well-formed origins pass.</summary>
    /// <param name="origin">An origin as configured.</param>
    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("https://app.example.com:8443")]
    [InlineData("http://127.0.0.1:5180")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://[::1]:5180")]
    public void WellFormedOriginsPass(string origin) => Validate(origin).Succeeded.ShouldBeTrue();

    /// <summary>With no origin at all the validator passes: an empty list denies cross-origin traffic.</summary>
    [Fact]
    public void NoOriginsPasses() =>
        new ApiCorsOptionsValidator().Validate(null, new ApiOptions()).Succeeded.ShouldBeTrue();

    /// <summary>Origins a browser never sends, or that allow everything, fail and name their key.</summary>
    /// <param name="origin">An origin as configured.</param>
    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("app.example.com")]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com/path")]
    [InlineData("https://app.example.com?x=1")]
    [InlineData("https://app.example.com:443")]
    [InlineData("https://user@app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("http://app.example.com")]
    public void MalformedOriginsFail(string origin)
    {
        var result = Validate(origin);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldNotBeNull().Single().ShouldStartWith("Api:Cors:Origins:0 ", Case.Sensitive);
    }

    /// <summary>The index of the failing entry is named, so a long list can be fixed.</summary>
    [Fact]
    public void TheFailingIndexIsNamed()
    {
        var options = new ApiOptions();
        options.Cors.Origins.AddRange(["https://app.example.com", "https://other.example.com/"]);

        var result = new ApiCorsOptionsValidator().Validate(null, options);

        result.Failures.ShouldNotBeNull().Single().ShouldStartWith("Api:Cors:Origins:1 ", Case.Sensitive);
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(string origin)
    {
        var options = new ApiOptions();
        options.Cors.Origins.Add(origin);
        return new ApiCorsOptionsValidator().Validate(null, options);
    }
}
