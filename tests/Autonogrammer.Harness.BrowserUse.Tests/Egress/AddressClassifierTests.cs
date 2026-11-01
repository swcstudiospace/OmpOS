using Autonogrammer.Harness.BrowserUse.Egress;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Egress;

/// <summary>Denied ranges and encodings, plus public controls. Ambiguous literals are refused.</summary>
public sealed class AddressClassifierTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]
    [InlineData("127.1")]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("192.0.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("::1")]
    [InlineData("[::1]")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd00::1")]
    [InlineData("ff02::1")]
    [InlineData("::")]
    public void DeniedRanges_AreRefused(string literal)
    {
        AddressClassifier.ClassifyLiteral(literal).IsRefused.ShouldBeTrue();
    }
    [Theory]
    [InlineData("0x7f.0.0.1")]
    [InlineData("0x7f000001")]
    [InlineData("2130706433")]
    [InlineData("0xa9.0xfe.0xa9.0xfe")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.1.2.3")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("00.00.00.00")]
    public void Encodings_AreNormalisedThenClassified(string literal)
    {
        AddressClassifier.ClassifyLiteral(literal).IsRefused.ShouldBeTrue();
    }

    [Theory]
    [InlineData("64:ff9b::8.8.8.8")]
    [InlineData("::ffff:8.8.8.8")]
    public void PublicEncodings_StayAllowed(string literal)
    {
        AddressClassifier.ClassifyLiteral(literal).IsRefused.ShouldBeFalse();
    }

    [Fact]
    public void MappedAndNat64_UnwrapAndRecheckTheInnerAddress()
    {
        AddressClassifier.ClassifyLiteral("::ffff:8.8.8.8").Class.ShouldBe(AddressClass.Allowed);
        AddressClassifier.ClassifyLiteral("::ffff:192.168.0.1").Class.ShouldBe(AddressClass.Denied);
        AddressClassifier.ClassifyLiteral("64:ff9b::808:808").Class.ShouldBe(AddressClass.Allowed);
        AddressClassifier.ClassifyLiteral("64:ff9b::7f00:1").Reason.ShouldContain("loopback");
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("9.9.9.9")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("11.0.0.1")]
    [InlineData("2001:4860:4860::8888")]
    public void PublicControls_AreAllowed(string literal)
    {
        AddressClassifier.ClassifyLiteral(literal).Class.ShouldBe(AddressClass.Allowed);
    }

    [Theory]
    [InlineData("0127.0.0.1")]
    [InlineData("010.0.0.1")]
    [InlineData("0177.0.0.1")]
    [InlineData("0127")]
    public void MultiReadingLiterals_AreRefused(string literal)
    {
        var result = AddressClassifier.ClassifyLiteral(literal);
        result.Class.ShouldBe(AddressClass.Ambiguous);
        result.IsRefused.ShouldBeTrue();
    }
}
