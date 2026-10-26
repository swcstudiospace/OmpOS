using OmpOS.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings.Models;

/// <summary>Every custom provider rule, with accepted and rejected examples. The rules mirror the provider registry's validation.</summary>
public sealed class CustomProviderRulesTests
{
    private static readonly string[] Taken = ["ollama-local", "openai"];

    /// <summary>A blank display name is rejected; any other text is accepted.</summary>
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("My GPU box", true)]
    public void DisplayNameMustBePresent(string name, bool accepted)
    {
        var error = CustomProviderRules.ValidateDisplayName(name);

        (error is null).ShouldBe(accepted);
        if (!accepted)
        {
            error.ShouldBe(ModelsCopy.ErrDisplayNameRequired);
        }
    }

    /// <summary>An id is a lowercase slug of at most 64 characters.</summary>
    [Theory]
    [InlineData("my-gateway", true)]
    [InlineData("a", true)]
    [InlineData("gpu-box-2", true)]
    [InlineData("0day", true)]
    [InlineData("My-Gateway", false)]
    [InlineData("-lead", false)]
    [InlineData("trail-", false)]
    [InlineData("double--dash", false)]
    [InlineData("under_score", false)]
    [InlineData("has space", false)]
    [InlineData("dot.ted", false)]
    [InlineData("caf\u00e9", false)]
    public void IdMustBeALowercaseSlug(string id, bool accepted)
    {
        var error = CustomProviderRules.ValidateId(id, Taken);

        (error is null).ShouldBe(accepted);
        if (!accepted)
        {
            error.ShouldBe(ModelsCopy.ErrIdInvalid);
        }
    }

    /// <summary>A blank id is its own message; 64 characters pass and 65 fail.</summary>
    [Fact]
    public void IdBlankAndLengthBounds()
    {
        CustomProviderRules.ValidateId("  ", Taken).ShouldBe(ModelsCopy.ErrIdRequired);
        CustomProviderRules.ValidateId(new string('a', 64), Taken).ShouldBeNull();
        CustomProviderRules.ValidateId(new string('a', 65), Taken).ShouldBe(ModelsCopy.ErrIdInvalid);
    }

    /// <summary>An id another provider uses is rejected, whatever the case of the other id; the provider's own id is free when it is not in the list.</summary>
    [Fact]
    public void IdMustBeUnique()
    {
        CustomProviderRules.ValidateId("openai", Taken).ShouldBe(ModelsCopy.ErrIdTaken);
        CustomProviderRules.ValidateId("ollama-local", ["OLLAMA-LOCAL"]).ShouldBe(ModelsCopy.ErrIdTaken);
        CustomProviderRules.ValidateId("my-gateway", Taken).ShouldBeNull();
        CustomProviderRules.ValidateId("openai", ["ollama-local"]).ShouldBeNull();
    }

    /// <summary>The endpoint must be https, or http to loopback; user information, a query or a fragment is refused.</summary>
    [Theory]
    [InlineData("https://example.com/v1", false, null)]
    [InlineData("https://example.com", false, null)]
    [InlineData("http://localhost:11434/v1", false, null)]
    [InlineData("http://127.0.0.1:8080", false, null)]
    [InlineData("http://[::1]:8080/v1", false, null)]
    [InlineData("HTTP://LOCALHOST/v1", false, null)]
    [InlineData("http://example.com/v1", true, null)]
    [InlineData("", false, nameof(ModelsCopy.ErrEndpointRequired))]
    [InlineData("   ", false, nameof(ModelsCopy.ErrEndpointRequired))]
    [InlineData("example.com/v1", false, nameof(ModelsCopy.ErrEndpointNotAbsolute))]
    [InlineData("ftp://example.com/v1", false, nameof(ModelsCopy.ErrEndpointUnsupportedScheme))]
    [InlineData("ftp://example.com/v1", true, nameof(ModelsCopy.ErrEndpointUnsupportedScheme))]
    [InlineData("http://example.com/v1", false, nameof(ModelsCopy.ErrEndpointPlainHttpRemote))]
    [InlineData("http://127.0.0.2/v1", false, nameof(ModelsCopy.ErrEndpointPlainHttpRemote))]
    [InlineData("https://user:pw@example.com/v1", false, nameof(ModelsCopy.ErrEndpointCredential))]
    [InlineData("https://user@example.com/v1", true, nameof(ModelsCopy.ErrEndpointCredential))]
    [InlineData("https://example.com/v1?key=abc", false, nameof(ModelsCopy.ErrEndpointCredential))]
    [InlineData("https://example.com/v1#token", false, nameof(ModelsCopy.ErrEndpointCredential))]
    public void EndpointRules(string text, bool allowRemotePlainHttp, string? expectedCopyName)
    {
        var error = CustomProviderRules.ValidateEndpoint(text, allowRemotePlainHttp, out var endpoint);

        if (expectedCopyName is null)
        {
            error.ShouldBeNull();
            endpoint.ShouldNotBeNull();
        }
        else
        {
            error.ShouldBe(CopyByName(expectedCopyName));
            endpoint.ShouldBeNull();
        }
    }

    /// <summary>Model ids are one per line; blank lines and surrounding white space are dropped; CRLF is accepted.</summary>
    [Fact]
    public void ModelsAreParsedOnePerLine()
    {
        CustomProviderRules.ParseModels("fast\r\n\r\n  big  \nsmall").ShouldBe(["fast", "big", "small"]);
        CustomProviderRules.ParseModels(string.Empty).ShouldBeEmpty();
        CustomProviderRules.ParseModels(null).ShouldBeEmpty();
        CustomProviderRules.ValidateModels("fast\r\nbig").ShouldBeNull();
        CustomProviderRules.ValidateModels(string.Empty).ShouldBeNull();
    }

    /// <summary>A model id with white space or a control character inside, or longer than 200 characters, is rejected.</summary>
    [Theory]
    [InlineData("gpt 4")]
    [InlineData("a\tb")]
    [InlineData("a\u0001b")]
    [InlineData("a\u00a0b")]
    public void ModelIdsMayNotHoldWhiteSpaceOrControlCharacters(string text)
    {
        CustomProviderRules.ValidateModels($"ok\n{text}").ShouldBe(ModelsCopy.ErrModelInvalid);
    }

    /// <summary>A model id of 200 characters passes; 201 fails.</summary>
    [Fact]
    public void ModelIdLengthBounds()
    {
        CustomProviderRules.ValidateModels(new string('m', 200)).ShouldBeNull();
        CustomProviderRules.ValidateModels(new string('m', 201)).ShouldBe(ModelsCopy.ErrModelInvalid);
    }

    /// <summary>A model id listed twice is rejected; ids that differ only in case are different ids.</summary>
    [Fact]
    public void ModelIdsMayNotRepeat()
    {
        CustomProviderRules.ValidateModels("fast\nbig\nfast").ShouldBe(ModelsCopy.ErrModelDuplicate);
        CustomProviderRules.ValidateModels("Fast\nfast").ShouldBeNull();
    }

    /// <summary>An invalid id is reported before a repeat when both occur.</summary>
    [Fact]
    public void InvalidModelIdIsReportedBeforeADuplicate()
    {
        CustomProviderRules.ValidateModels("a\na\nb c").ShouldBe(ModelsCopy.ErrModelInvalid);
    }

    private static string CopyByName(string name) => name switch
    {
        nameof(ModelsCopy.ErrEndpointRequired) => ModelsCopy.ErrEndpointRequired,
        nameof(ModelsCopy.ErrEndpointNotAbsolute) => ModelsCopy.ErrEndpointNotAbsolute,
        nameof(ModelsCopy.ErrEndpointUnsupportedScheme) => ModelsCopy.ErrEndpointUnsupportedScheme,
        nameof(ModelsCopy.ErrEndpointPlainHttpRemote) => ModelsCopy.ErrEndpointPlainHttpRemote,
        nameof(ModelsCopy.ErrEndpointCredential) => ModelsCopy.ErrEndpointCredential,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown copy name in the test data."),
    };
}
