using Xunit;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Shouldly;

namespace Autonogrammer.Contracts.Tests.Dispatch;

/// <summary>Closed sets and the one-object frame guard.</summary>
public sealed class DispatchFrameGuardTests
{
    /// <summary>A single object with v and t is admitted.</summary>
    [Fact]
    public void AcceptsOneObject()
    {
        var ok = DispatchFrameGuard.TryAdmit("{\"v\":1,\"t\":\"hb\"}"u8, out var admission);

        ok.ShouldBeTrue();
        admission.Accepted.ShouldBeTrue();
        admission.Type.ShouldBe("hb");
        admission.ErrCode.ShouldBeNull();
    }

    /// <summary>Invalid UTF-8 is binary-shaped and refused.</summary>
    [Fact]
    public void RejectsBinaryShapedInput()
    {
        var ok = DispatchFrameGuard.TryAdmit([(byte)0xFF, (byte)0xFE], out var admission);

        ok.ShouldBeFalse();
        admission.ErrCode.ShouldBeNull();
    }

    /// <summary>A duplicate property name is refused.</summary>
    [Fact]
    public void RejectsDuplicatePropertyNames()
    {
        var ok = DispatchFrameGuard.TryAdmit("{\"v\":1,\"t\":\"hb\",\"v\":1}"u8, out _);

        ok.ShouldBeFalse();
    }

    /// <summary>An unknown t is an unsupported_frame error at the type level and is not executed.</summary>
    [Fact]
    public void UnknownTypeIsUnsupportedFrame()
    {
        var ok = DispatchFrameGuard.TryAdmit("{\"v\":1,\"t\":\"computer.task\"}"u8, out var admission);

        ok.ShouldBeFalse();
        admission.ErrCode.ShouldBe(DispatchFrameTypes.UnsupportedFrame);
        var err = DispatchFrameGuard.UnsupportedFrame();
        err.T.ShouldBe("err");
        err.Code.ShouldBe("unsupported_frame");
        DispatchCloseCodes.PolicyViolation.ShouldBe(1008);
    }

    /// <summary>Two JSON values are not one object.</summary>
    [Fact]
    public void RejectsTwoValues()
    {
        var ok = DispatchFrameGuard.TryAdmit("{\"v\":1,\"t\":\"hb\"}{}"u8, out _);

        ok.ShouldBeFalse();
    }

    /// <summary>DispatchOp parses the seven closed names and no computer.* value.</summary>
    [Fact]
    public void OpSetIsClosed()
    {
        DispatchOpWire.TryParse("session.list", out _).ShouldBeTrue();
        DispatchOpWire.TryParse("session.steer", out _).ShouldBeTrue();
        DispatchOpWire.TryParse("session.cancel", out _).ShouldBeTrue();
        DispatchOpWire.TryParse("approval.decide", out _).ShouldBeTrue();
        DispatchOpWire.TryParse("code.prompt", out _).ShouldBeTrue();
        DispatchOpWire.TryParse("cowork.task", out _).ShouldBeTrue();
        DispatchOpWire.TryParse("command.cancel", out _).ShouldBeTrue();
        DispatchOpWire.TryParse("computer.task", out _).ShouldBeFalse();
        DispatchOpWire.TryParse("computer.click", out _).ShouldBeFalse();
        Enum.GetNames<DispatchOp>().ShouldNotContain(name => name.Contains("Computer", StringComparison.Ordinal));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize("\"computer.task\""u8, DispatchJsonContext.Default.DispatchOp));
    }

    /// <summary>Capabilities parse the five names, and tool classes are not command capabilities.</summary>
    [Fact]
    public void CapabilitySetIsClosed()
    {
        DispatchCapabilityWire.TryParse("sessions", out var sessions).ShouldBeTrue();
        DispatchCapabilityWire.IsCommandCapability(sessions).ShouldBeTrue();
        DispatchCapabilityWire.TryParse("code", out _).ShouldBeTrue();
        DispatchCapabilityWire.TryParse("cowork", out _).ShouldBeTrue();
        DispatchCapabilityWire.TryParse("browser_use", out var browser).ShouldBeTrue();
        DispatchCapabilityWire.TryParse("computer_use", out var computer).ShouldBeTrue();
        DispatchCapabilityWire.IsCommandCapability(browser).ShouldBeFalse();
        DispatchCapabilityWire.IsCommandCapability(computer).ShouldBeFalse();
        DispatchCapabilityWire.TryParse("computer.task", out _).ShouldBeFalse();
    }

    /// <summary>Failure reasons are the seventeen closed names.</summary>
    [Fact]
    public void FailureReasonsAreClosed()
    {
        var names = new[]
        {
            "expired", "revoked", "signature_invalid", "unsupported_version", "unsupported_operation",
            "capability_disabled", "approval_denied", "approval_timeout", "policy_denied", "payload_invalid",
            "payload_too_large", "device_busy", "device_restarted", "execution_error", "timeout",
            "cancelled_by_user", "internal",
        };
        names.Length.ShouldBe(17);
        Enum.GetValues<DispatchFailureReason>().Length.ShouldBe(17);
        foreach (var name in names)
        {
            DispatchFailureReasonWire.TryParse(name, out _).ShouldBeTrue();
        }

        DispatchFailureReasonWire.TryParse("unsupported_frame", out _).ShouldBeFalse();
    }

    /// <summary>Close codes are the closed set.</summary>
    [Fact]
    public void CloseCodesAreClosed()
    {
        int[] codes = [1000, 1001, 1007, 1008, 1009, 1011, 1013, 4001, 4002, 4009, 4010, 4011, 4020];
        foreach (var code in codes)
        {
            DispatchCloseCodes.IsKnown(code).ShouldBeTrue();
        }

        DispatchCloseCodes.IsKnown(4000).ShouldBeFalse();
    }

    /// <summary>Hub states are the ten closed names.</summary>
    [Fact]
    public void HubStatesAreClosed()
    {
        Enum.GetValues<DispatchCommandState>().Length.ShouldBe(10);
        DispatchCommandStateWire.TryParse("awaiting_approval", out _).ShouldBeTrue();
        DispatchCommandStateWire.TryParse("duplicate", out _).ShouldBeFalse();
    }

    /// <summary>Frames and DTOs round-trip through the source-generated context.</summary>
    [Fact]
    public void JsonContextRoundTripsAnErrorFrame()
    {
        var frame = DispatchFrameGuard.UnsupportedFrame();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, DispatchJsonContext.Default.DispatchErrFrame);
        Encoding.UTF8.GetString(bytes).ShouldContain("\"t\":\"err\"", Case.Sensitive);
        var back = JsonSerializer.Deserialize(bytes, DispatchJsonContext.Default.DispatchErrFrame);
        back.ShouldNotBeNull();
        back.Code.ShouldBe("unsupported_frame");
    }
}
