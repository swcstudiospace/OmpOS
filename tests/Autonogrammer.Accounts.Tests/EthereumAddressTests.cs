using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Tests;

/// <summary>The EIP-55 checksum of a wallet address, against the published EIP-55 examples and the malformed text a wallet can return.</summary>
public sealed class EthereumAddressTests
{
    /// <summary>Verifies each published EIP-55 example is its own checksum from its checksum form and from all lowercase.</summary>
    /// <param name="checksum">The example in the checksum form the specification gives it.</param>
    [Theory]
    [InlineData("0x52908400098527886E0F7030069857D2E4169EE7")]
    [InlineData("0x8617E340B3D01FA5F11F306F4090FD50E238070D")]
    [InlineData("0xde709f2102306220921060314715629080e2fb77")]
    [InlineData("0x27b1fdb04752bbc536007a920d24acb045561c26")]
    [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]
    [InlineData("0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359")]
    [InlineData("0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB")]
    [InlineData("0xD1220A0cf47c7B9Be7A2E6BA89F429762e7b9aDb")]
    public void EachEip55ExampleIsItsOwnChecksumFromAnyCase(string checksum)
    {
        EthereumAddress.TryToChecksumAddress(checksum, out string? fromChecksum).ShouldBeTrue();
        EthereumAddress.TryToChecksumAddress(Lowercase(checksum), out string? fromLowercase).ShouldBeTrue();

        fromChecksum.ShouldBe(checksum);
        fromLowercase.ShouldBe(checksum);
    }

    /// <summary>Verifies text that is not a 20-byte hex address with a lowercase 0x prefix is refused, and no checksum is returned.</summary>
    /// <param name="address">The malformed text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("0x")]
    [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAe")]
    [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAedd")]
    [InlineData("5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]
    [InlineData("0X5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]
    [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAeg")]
    [InlineData(null)]
    public void MalformedAddressesAreRefused(string? address)
    {
        EthereumAddress.TryToChecksumAddress(address, out string? checksum).ShouldBeFalse();

        checksum.ShouldBeNull();
    }

    private static string Lowercase(string text) =>
        new(text.Select(static c => c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c).ToArray());
}
