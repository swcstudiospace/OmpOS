using Autonogrammer.Api.Harness;
using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Harness;

/// <summary>
/// The start-up validation of <c>Harness:Acp:Approval</c>, run by <see cref="EngineIsolationOptionsValidator"/>: every kind must be an ACP tool kind,
/// entries are non-blank and unique, and the decision timeout is in range. Messages name keys and the offending kind, never a tool name.
/// </summary>
public sealed class HarnessApprovalOptionsValidationTests
{
    /// <summary>The defaults are valid and deny everything: no kind, no grantable tool, the shared decision window.</summary>
    [Fact]
    public void DefaultsAreValidAndDenyEverything()
    {
        var options = ConfigWith(new HarnessApprovalOptions());

        Validate(options).Succeeded.ShouldBeTrue();
        var approval = options.Acp.Approval;
        approval.Kinds.ShouldBeEmpty();
        approval.GrantableTools.ShouldBeEmpty();
        approval.DecisionTimeout.ShouldBe(AcpHarnessSessionFactory.PermissionDecisionTimeout);
        approval.CreatePolicy().ShouldBeSameAs(AcpApprovalPolicy.DenyAll);
    }

    /// <summary>The single-owner recipe is valid: every action kind, and the wildcard ceiling.</summary>
    [Fact]
    public void SingleOwnerRecipeIsValid()
    {
        var approval = new HarnessApprovalOptions
        {
            Kinds = ["read", "search", "edit", "delete", "move", "execute"],
            GrantableTools = ["*"],
        };

        Validate(ConfigWith(approval)).Succeeded.ShouldBeTrue();
        var policy = approval.CreatePolicy();
        policy.Allows(new AcpPermissionRequest("call", "tool", "execute", "{}")).ShouldBeTrue();
        policy.Allows(new AcpPermissionRequest("call", "tool", "fetch", "{}")).ShouldBeFalse();
    }

    /// <summary>Every ACP tool kind is accepted.</summary>
    [Fact]
    public void EveryAcpToolKindIsAccepted()
    {
        var approval = new HarnessApprovalOptions { Kinds = [.. HarnessApprovalOptions.AcpToolKinds] };

        Validate(ConfigWith(approval)).Succeeded.ShouldBeTrue();
        HarnessApprovalOptions.AcpToolKinds.ShouldBe(["read", "edit", "delete", "move", "search", "execute", "think", "fetch", "switch_mode", "other"]);
    }

    /// <summary>An unknown kind, a different case and a blank kind are refused, each naming its key.</summary>
    [Theory]
    [InlineData("shell", "Harness:Acp:Approval:Kinds:0 'shell' is not an ACP tool kind")]
    [InlineData("Execute", "Harness:Acp:Approval:Kinds:0 'Execute' is not an ACP tool kind")]
    [InlineData("*", "Harness:Acp:Approval:Kinds:0 '*' is not an ACP tool kind")]
    [InlineData("", "Harness:Acp:Approval:Kinds:0 must not be blank")]
    [InlineData("  ", "Harness:Acp:Approval:Kinds:0 must not be blank")]
    public void InvalidKindIsRefused(string kind, string expected)
    {
        var result = Validate(ConfigWith(new HarnessApprovalOptions { Kinds = [kind] }));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(expected);
    }

    /// <summary>A kind listed twice is refused at its second position.</summary>
    [Fact]
    public void DuplicateKindIsRefused()
    {
        var result = Validate(ConfigWith(new HarnessApprovalOptions { Kinds = ["execute", "read", "execute"] }));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Harness:Acp:Approval:Kinds:2 'execute' is listed more than once");
    }

    /// <summary>A blank or repeated grantable tool is refused, and the message never echoes the tool name.</summary>
    [Fact]
    public void BlankAndDuplicateGrantableToolsAreRefusedWithoutEchoingNames()
    {
        var blank = Validate(ConfigWith(new HarnessApprovalOptions { GrantableTools = ["bash", " "] }));
        var duplicate = Validate(ConfigWith(new HarnessApprovalOptions { GrantableTools = ["secret-tool-name", "secret-tool-name"] }));

        blank.Failed.ShouldBeTrue();
        blank.FailureMessage.ShouldContain("Harness:Acp:Approval:GrantableTools:1 must not be blank");
        duplicate.Failed.ShouldBeTrue();
        duplicate.FailureMessage.ShouldContain("Harness:Acp:Approval:GrantableTools:1 is listed more than once");
        duplicate.FailureMessage.ShouldNotContain("secret-tool-name");
    }

    /// <summary>A decision timeout outside 1 to the maximum is refused; the bounds themselves are accepted.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(1, true)]
    [InlineData(300, true)]
    [InlineData(HarnessApprovalOptions.MaxDecisionTimeoutSeconds, true)]
    [InlineData(HarnessApprovalOptions.MaxDecisionTimeoutSeconds + 1, false)]
    public void DecisionTimeoutMustBeInRange(int seconds, bool valid)
    {
        var result = Validate(ConfigWith(new HarnessApprovalOptions { DecisionTimeoutSeconds = seconds }));

        result.Succeeded.ShouldBe(valid);
        if (!valid)
        {
            result.FailureMessage.ShouldNotBeNull().ShouldContain("Harness:Acp:Approval:DecisionTimeoutSeconds must be between 1 and");
        }
    }

    /// <summary>Several mistakes are all reported at once.</summary>
    [Fact]
    public void AllFailuresAreReportedTogether()
    {
        var result = Validate(ConfigWith(new HarnessApprovalOptions { Kinds = ["shell"], GrantableTools = [""], DecisionTimeoutSeconds = 0 }));

        result.Failures.ShouldNotBeNull().Count().ShouldBe(3);
    }

    /// <summary>The approval keys are checked for the in-memory adapter too, so a stray bad key cannot surface later as a run failure.</summary>
    [Fact]
    public void ApprovalKeysAreCheckedForTheInMemoryAdapter()
    {
        var options = new HarnessOptions { Adapter = HarnessOptions.InMemoryAdapter, Acp = new HarnessOptions.AcpSettings { Approval = new HarnessApprovalOptions { Kinds = ["shell"] } } };

        Validate(options).Failed.ShouldBeTrue();
    }

    /// <summary>The settings the runs receive carry the configured ceiling and timeout.</summary>
    [Fact]
    public void ToolApprovalSettingsFollowTheConfiguration()
    {
        var approval = new HarnessApprovalOptions { GrantableTools = ["bash", "edit"], DecisionTimeoutSeconds = 90 };

        var settings = new HarnessToolApprovalSettings(Microsoft.Extensions.Options.Options.Create(ConfigWith(approval)));

        settings.DecisionTimeout.ShouldBe(TimeSpan.FromSeconds(90));
        settings.GrantableTools.OrderBy(static tool => tool, StringComparer.Ordinal).ShouldBe(["bash", "edit"]);
    }

    private static HarnessOptions ConfigWith(HarnessApprovalOptions approval) => new()
    {
        Adapter = HarnessOptions.AcpAdapter,
        Acp = new HarnessOptions.AcpSettings { Approval = approval },
    };

    private static ValidateOptionsResult Validate(HarnessOptions options) =>
        new EngineIsolationOptionsValidator(TestHostEnvironment.Development, Microsoft.Extensions.Options.Options.Create(new WorktreesOptions())).Validate(null, options);
}
