using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>The approval policy is deny-by-default and can never grant beyond its allowlist.</summary>
public sealed class AcpApprovalPolicyTests
{
    /// <summary>The default policy denies every kind.</summary>
    [Fact]
    public void DenyAllDeniesEveryKind()
    {
        var policy = AcpApprovalPolicy.DenyAll;

        policy.Allows(Request("read")).ShouldBeFalse();
        policy.Allows(Request("execute")).ShouldBeFalse();
        policy.Allows(Request(string.Empty)).ShouldBeFalse();
    }

    /// <summary>Only allowlisted kinds are granted.</summary>
    [Fact]
    public void AllowlistGrantsOnlyListedKinds()
    {
        var policy = new AcpApprovalPolicy(["read"]);

        policy.Allows(Request("read")).ShouldBeTrue();
        policy.Allows(Request("execute")).ShouldBeFalse();
    }

    /// <summary>A request with no kind is denied even when the allowlist holds the empty string.</summary>
    [Fact]
    public void EmptyKindIsNeverGranted()
    {
        var policy = new AcpApprovalPolicy([string.Empty]);

        policy.Allows(Request(string.Empty)).ShouldBeFalse();
    }

    /// <summary>The narrowing hook is not consulted for a kind outside the allowlist, so it cannot grant it.</summary>
    [Fact]
    public void NarrowingHookCannotGrantNonAllowlistedKind()
    {
        var consulted = 0;
        var policy = new AcpApprovalPolicy(["read"], request =>
        {
            consulted++;
            return true;
        });

        policy.Allows(Request("execute")).ShouldBeFalse();
        consulted.ShouldBe(0);
    }

    /// <summary>The narrowing hook can refuse a request the allowlist admits.</summary>
    [Fact]
    public void NarrowingHookCanRefuseAllowlistedKind()
    {
        var policy = new AcpApprovalPolicy(["read"], static _ => false);

        policy.Allows(Request("read")).ShouldBeFalse();
    }

    /// <summary>The narrowing hook receives the request it is judging.</summary>
    [Fact]
    public void NarrowingHookReceivesTheRequest()
    {
        var seen = new List<AcpPermissionRequest>();
        var policy = new AcpApprovalPolicy(["read"], request =>
        {
            seen.Add(request);
            return true;
        });

        policy.Allows(Request("read")).ShouldBeTrue();
        seen.ShouldBe([Request("read")]);
    }

    private static AcpPermissionRequest Request(string kind) => new("call-1", "title", kind, "{}");
}
