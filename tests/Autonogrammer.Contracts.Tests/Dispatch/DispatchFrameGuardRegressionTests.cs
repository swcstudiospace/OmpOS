using Autonogrammer.Contracts.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests.Dispatch;

/// <summary>Only an unknown <c>t</c> is answered with <c>unsupported_frame</c>. A known <c>t</c> that fails a later check has no error code, so the hub closes it as invalid_frame.</summary>
public sealed class DispatchFrameGuardRegressionTests
{
    /// <summary>A known t followed by an unsupported v is refused without an error code.</summary>
    [Fact]
    public void KnownTypeWithBadVersionCarriesNoErrorCode()
    {
        var ok = DispatchFrameGuard.TryAdmit("{\"t\":\"hb\",\"v\":2}"u8, out var admission);

        ok.ShouldBeFalse();
        admission.Accepted.ShouldBeFalse();
        admission.Type.ShouldBe("hb");
        admission.ErrCode.ShouldBeNull();
    }

    /// <summary>A known t followed by a duplicate property is refused without an error code.</summary>
    [Fact]
    public void KnownTypeWithDuplicatePropertyCarriesNoErrorCode()
    {
        var ok = DispatchFrameGuard.TryAdmit("{\"t\":\"hb\",\"v\":1,\"v\":1}"u8, out var admission);

        ok.ShouldBeFalse();
        admission.ErrCode.ShouldBeNull();
    }

    /// <summary>An unknown t keeps unsupported_frame even when the rest of the frame is also invalid.</summary>
    [Fact]
    public void UnknownTypeWithBadVersionStillCarriesUnsupportedFrame()
    {
        var ok = DispatchFrameGuard.TryAdmit("{\"t\":\"computer.task\",\"v\":2}"u8, out var admission);

        ok.ShouldBeFalse();
        admission.Type.ShouldBe("computer.task");
        admission.ErrCode.ShouldBe(DispatchFrameTypes.UnsupportedFrame);
    }
}
