using System.Net;
using Autonogrammer.Api.Services.Providers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Providers;

/// <summary>The two address guards: never a link-local or metadata address as an endpoint, and never anything but a public address for a live test.</summary>
public sealed class PublicAddressPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("100.100.100.200")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("192.0.2.5")]
    [InlineData("198.18.0.1")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("fd00:ec2::254")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("2002:c0a8:101::1")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("2001:db8::1")]
    public void AnAddressOffThePublicInternetIsNotPublic(string text)
    {
        PublicAddressPolicy.IsPublic(IPAddress.Parse(text)).ShouldBeFalse(text);
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("203.0.114.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void APublicAddressIsPublic(string text)
    {
        PublicAddressPolicy.IsPublic(IPAddress.Parse(text)).ShouldBeTrue(text);
    }

    [Theory]
    [InlineData("169.254.1.1", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("::ffff:169.254.169.254", true)]
    [InlineData("fe80::1", true)]
    [InlineData("100.100.100.200", true)]
    [InlineData("168.63.129.16", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("224.0.0.1", true)]
    [InlineData("fd00:ec2::254", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("8.8.8.8", false)]
    public void OnlyALinkLocalMetadataOrReservedAddressIsNeverAnEndpoint(string text, bool expected)
    {
        PublicAddressPolicy.IsLinkLocalOrMetadata(IPAddress.Parse(text)).ShouldBe(expected, text);
    }
}
