using Autonogrammer.Cowork.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Abstractions;

/// <summary>The Abstractions assembly holds the D5 types plus the scaffold marker and nothing else.</summary>
public sealed class CoworkContractShapeTests
{
    private static readonly string[] Expected =
    [
        nameof(AssemblyMarker),
        nameof(CoworkTurnOrigin),
        nameof(CoworkToolEffect),
        nameof(CoworkApprovalRequirement),
        nameof(CoworkResultKinds),
        nameof(CoworkProviderAvailability),
        nameof(CoworkPolicyOutcome),
        nameof(CoworkToolEventKind),
        nameof(CoworkApprovalOutcome),
        nameof(CoworkToolDescriptor),
        nameof(CoworkSessionContext),
        nameof(CoworkToolInvocation),
        nameof(CoworkGrantKey),
        nameof(CoworkProviderState),
        nameof(CoworkCallPreview),
        nameof(CoworkContentBlock),
        nameof(CoworkTextBlock),
        nameof(CoworkImageBlock),
        nameof(CoworkToolResult),
        nameof(CoworkPolicyRequest),
        nameof(CoworkPolicyDecision),
        nameof(CoworkToolEvent),
        nameof(CoworkApprovalRequest),
        nameof(CoworkApprovalDecision),
        nameof(CoworkToolAuditEntry),
        nameof(ICoworkToolProvider),
        nameof(ICoworkSessionLease),
        nameof(ICoworkToolHost),
        nameof(ICoworkPolicyGate),
        nameof(ICoworkApprovalBroker),
        nameof(ICoworkGrantStore),
        nameof(ICoworkToolAudit),
    ];

    [Fact]
    public void AssemblyHoldsExactlyTheD5TypesPlusAssemblyMarker()
    {
        var names = typeof(Autonogrammer.Cowork.Abstractions.AssemblyMarker).Assembly.GetTypes()
            .Where(type => type.IsPublic)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        names.ShouldBe(Expected.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void EnumOrdinalsMatchTheDecision()
    {
        ((int)CoworkTurnOrigin.Local).ShouldBe(0);
        ((int)CoworkTurnOrigin.Dispatch).ShouldBe(1);
        ((int)CoworkToolEffect.Destructive).ShouldBe(2);
        ((int)CoworkApprovalRequirement.ByProviderGate).ShouldBe(3);
        ((int)CoworkResultKinds.Text).ShouldBe(1);
        ((int)CoworkResultKinds.Image).ShouldBe(2);
        ((int)CoworkProviderAvailability.Unavailable).ShouldBe(2);
        ((int)CoworkPolicyOutcome.Deny).ShouldBe(2);
        ((int)CoworkToolEventKind.Denied).ShouldBe(4);
        ((int)CoworkApprovalOutcome.Unavailable).ShouldBe(4);
    }

    [Fact]
    public void DescriptorResultsDefaultToText()
    {
        var descriptor = new CoworkToolDescriptor
        {
            Name = "artifact_create",
            ProviderId = "artifact",
            Description = "Create",
            InputSchema = System.Text.Json.JsonDocument.Parse("{}").RootElement,
            Effect = CoworkToolEffect.Actuating,
            Approval = CoworkApprovalRequirement.ByPolicy,
        };
        descriptor.Results.ShouldBe(CoworkResultKinds.Text);
        CoworkToolResult.Failure("The tool call failed.").IsError.ShouldBeTrue();
        CoworkToolResult.Text("ok").IsError.ShouldBeFalse();
    }
}
