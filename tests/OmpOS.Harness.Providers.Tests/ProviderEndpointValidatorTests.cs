using Shouldly;
using Xunit;

namespace OmpOS.Harness.Providers.Tests;

/// <summary>Verifies the endpoint rules through accept and refuse outcomes for each scheme and host form.</summary>
public sealed class ProviderEndpointValidatorTests
{
    [Fact]
    public void HttpsEndpointIsAccepted()
    {
        // Row: providers-local-us.md:15 (Ollama cloud base https://ollama.com/v1).
        var result = ProviderEndpointValidator.Validate(VerifiedProviderRows.OllamaCloudOpenAi.BaseEndpoint, allowRemotePlainHttp: false);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void LocalhostHttpIsAccepted()
    {
        // Row: providers-local-us.md:15 (Ollama local base http://localhost:11434/v1/).
        var result = ProviderEndpointValidator.Validate(VerifiedProviderRows.OllamaLocalOpenAi.BaseEndpoint, allowRemotePlainHttp: false);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void IPv4LoopbackHttpIsAccepted()
    {
        // Row: providers-local-us.md:17 (llama.cpp default base http://127.0.0.1:9931).
        var result = ProviderEndpointValidator.Validate(VerifiedProviderRows.LlamaCppLocalOpenAi.BaseEndpoint, allowRemotePlainHttp: false);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void IPv6LoopbackHttpIsAccepted()
    {
        // Port and path follow the LM Studio row (providers-local-us.md:16, http://localhost:1234/v1). The host is the
        // IPv6 loopback literal ::1 in brackets, which the validator must accept whether Uri keeps or strips them.
        var endpoint = new Uri("http://[::1]:1234/v1");

        var result = ProviderEndpointValidator.Validate(endpoint, allowRemotePlainHttp: false);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("http://127.0.0.2:11434/v1")]
    [InlineData("http://localhost.example.test:11434/v1")]
    [InlineData("http://[::2]:1234/v1")]
    public void LoopbackLookalikesAreRefusedWithoutExplicitAllow(string endpoint)
    {
        // Only the exact hosts localhost, 127.0.0.1 and ::1 are loopback. Other addresses in 127/8 are not.
        var result = ProviderEndpointValidator.Validate(new Uri(endpoint), allowRemotePlainHttp: false);

        result.Errors.ShouldContain(ProviderValidationError.PlainHttpToRemoteHost);
    }

    [Fact]
    public void RemotePlainHttpIsRefusedByDefault()
    {
        // Row: providers-local-us.md:15 (Ollama cloud base https://ollama.com/v1) with the scheme downgraded to http.
        var endpoint = new Uri("http://ollama.com/v1");

        var result = ProviderEndpointValidator.Validate(endpoint, allowRemotePlainHttp: false);

        result.Errors.ShouldContain(ProviderValidationError.PlainHttpToRemoteHost);
    }

    [Fact]
    public void RemotePlainHttpIsAcceptedOnlyWithExplicitAllow()
    {
        // Same endpoint as above; the explicit allow flag is the only change.
        var endpoint = new Uri("http://ollama.com/v1");

        var result = ProviderEndpointValidator.Validate(endpoint, allowRemotePlainHttp: true);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("ftp://ollama.com/v1")]
    [InlineData("wss://ollama.com/v1")]
    [InlineData("file:///etc/providers")]
    public void OtherSchemesAreRefusedEvenWhenPlainHttpIsAllowed(string endpoint)
    {
        var uri = new Uri(endpoint);

        ProviderEndpointValidator.Validate(uri, allowRemotePlainHttp: false).Errors.ShouldContain(ProviderValidationError.UnsupportedScheme);
        ProviderEndpointValidator.Validate(uri, allowRemotePlainHttp: true).Errors.ShouldContain(ProviderValidationError.UnsupportedScheme);
    }

    [Theory]
    [InlineData("https://user:secret@api.example.test/v1")]
    [InlineData("https://user@api.example.test/v1")]
    [InlineData("https://api.example.test/v1?api_key=secret")]
    [InlineData("https://api.example.test/v1#key=secret")]
    [InlineData("http://user:secret@localhost:11434/v1")]
    public void CredentialBearingEndpointsAreRefused(string endpoint)
    {
        // A key placed in user information, a query or a fragment would be stored in the entry. The refusal holds even
        // when the scheme and host would otherwise be accepted, including the loopback http case.
        var result = ProviderEndpointValidator.Validate(new Uri(endpoint), allowRemotePlainHttp: false);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(ProviderValidationError.CredentialInEndpoint);
    }

    [Fact]
    public void RelativeEndpointIsRejected()
    {
        var endpoint = new Uri("/v1", UriKind.Relative);

        var result = ProviderEndpointValidator.Validate(endpoint, allowRemotePlainHttp: false);

        result.Errors.ShouldContain(ProviderValidationError.BaseEndpointNotAbsolute);
    }

    [Fact]
    public void MissingEndpointIsRejected()
    {
        var result = ProviderEndpointValidator.Validate(null, allowRemotePlainHttp: false);

        result.Errors.ShouldContain(ProviderValidationError.MissingBaseEndpoint);
    }
}
