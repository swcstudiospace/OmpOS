using System.Text;
using Autonogrammer.Contracts.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests.Dispatch;

/// <summary>The hello canonical bytes bind the nonce, the device, the origin the device reached and the last sequence it applied.</summary>
public sealed class DispatchHelloCanonicalTests
{
    /// <summary>The same fields build the same bytes, so the device and the hub sign and verify the same message.</summary>
    [Fact]
    public void The_same_fields_build_the_same_bytes()
    {
        var first = DispatchHelloCanonical.Build("abc_DEF-123", "device-1", "https://hub.example", 7);
        var second = DispatchHelloCanonical.Build("abc_DEF-123", "device-1", "https://hub.example", 7);

        Encoding.UTF8.GetString(first).ShouldBe(Encoding.UTF8.GetString(second));
    }

    /// <summary>A different origin changes the signed bytes, so a hello that reached another origin does not verify (origin binding).</summary>
    [Fact]
    public void A_different_origin_changes_the_signed_bytes()
    {
        var reached = DispatchHelloCanonical.Build("abc_DEF-123", "device-1", "https://hub.example", 7);
        var other = DispatchHelloCanonical.Build("abc_DEF-123", "device-1", "https://other.example", 7);

        string.Equals(Encoding.UTF8.GetString(other), Encoding.UTF8.GetString(reached), StringComparison.Ordinal).ShouldBeFalse();
    }

    /// <summary>A nonce outside the signed charset is refused before any bytes are built.</summary>
    [Fact]
    public void A_nonce_outside_the_signed_charset_is_refused()
    {
        Should.Throw<ArgumentException>(() => DispatchHelloCanonical.Build("nonce with spaces", "device-1", "https://hub.example", 7));
    }
}
