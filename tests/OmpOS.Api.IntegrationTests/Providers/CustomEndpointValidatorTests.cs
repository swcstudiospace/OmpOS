using OmpOS.Api.Services.Providers;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Providers;

/// <summary>The safety table of the base URL of a custom endpoint: what is refused in every spelling, and what is accepted and written canonically.</summary>
public sealed class CustomEndpointValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://llm.example.com/v1")]
    [InlineData("http://203.0.113.7/v1")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://169.254.1.1/")]
    [InlineData("https://metadata.google.internal/computeMetadata/v1")]
    [InlineData("https://METADATA.GOOGLE.INTERNAL./computeMetadata/v1")]
    [InlineData("https://sub.metadata.google.internal/x")]
    [InlineData("https://metadata.goog/")]
    [InlineData("https://instance-data/latest")]
    [InlineData("https://instance-data.ec2.internal/latest")]
    [InlineData("https://user:pass@llm.example.com/v1")]
    [InlineData("https://user@llm.example.com/v1")]
    [InlineData("https://llm.example.com@evil.example.net/v1")]
    [InlineData("http://2852039166/")]
    [InlineData("http://0251.0376.0251.0376/")]
    [InlineData("http://0xa9.0xfe.0xa9.0xfe/")]
    [InlineData("http://0xa9fea9fe/")]
    [InlineData("http://2130706433/")]
    [InlineData("http://127.1/")]
    [InlineData("https://[::ffff:169.254.169.254]/")]
    [InlineData("https://[::ffff:a9fe:a9fe]/")]
    [InlineData("https://[fe80::1]/")]
    [InlineData("https://[fd00:ec2::254]/")]
    [InlineData("https://[fe80::1%25eth0]/")]
    [InlineData("https://100.100.100.200/")]
    [InlineData("https://168.63.129.16/")]
    [InlineData("https://0.0.0.0/")]
    [InlineData("https://224.0.0.1/")]
    [InlineData("https://intranet/")]
    [InlineData("ftp://llm.example.com/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://llm.example.com/")]
    [InlineData("https://llm.example.com/v1?key=1")]
    [InlineData("https://llm.example.com/v1#frag")]
    [InlineData("https://llm.example.com/a b")]
    [InlineData("llm.example.com/v1")]
    public void AnUnsafeBaseUrlIsRefusedWithASentenceThatDoesNotEchoIt(string address)
    {
        var check = CustomEndpointValidator.Validate(address);

        check.Url.ShouldBeNull();
        var problem = check.Problem.ShouldNotBeNull();
        problem.ShouldNotBeNullOrWhiteSpace();
        if (address.Trim().Length > 8)
        {
            problem.ShouldNotContain(address.Trim(), Case.Insensitive);
        }
    }

    [Fact]
    public void AnOverlongBaseUrlIsRefused()
    {
        CustomEndpointValidator.Validate("https://llm.example.com/" + new string('a', CustomEndpointValidator.MaxLength)).Url.ShouldBeNull();
    }

    [Theory]
    [InlineData("https://llm.example.com/v1", "https://llm.example.com/v1")]
    [InlineData("https://llm.example.com/v1/", "https://llm.example.com/v1")]
    [InlineData("  https://LLM.Example.COM/v1  ", "https://llm.example.com/v1")]
    [InlineData("https://llm.example.com:8443/openai/v1", "https://llm.example.com:8443/openai/v1")]
    [InlineData("https://203.0.113.7/v1", "https://203.0.113.7/v1")]
    [InlineData("http://localhost:11434/v1", "http://localhost:11434/v1")]
    [InlineData("http://127.0.0.1:8080/v1", "http://127.0.0.1:8080/v1")]
    [InlineData("http://[::1]:8080/v1", "http://[::1]:8080/v1")]
    [InlineData("http://app.localhost:8080/v1", "http://app.localhost:8080/v1")]
    [InlineData("https://[2606:4700:4700::1111]/v1", "https://[2606:4700:4700::1111]/v1")]
    public void AnAcceptableBaseUrlIsWrittenCanonically(string address, string expected)
    {
        var check = CustomEndpointValidator.Validate(address);

        check.Problem.ShouldBeNull();
        check.Url.ShouldNotBeNull().AbsoluteUri.ShouldBe(expected);
    }
}
