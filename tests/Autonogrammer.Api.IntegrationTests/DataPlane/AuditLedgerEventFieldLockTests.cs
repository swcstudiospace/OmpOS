using System.Reflection;
using System.Text.Json;
using Autonogrammer.Api.Audit;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// The audit event is a closed type. The ledger cannot be scrubbed, so any new public property fails this test.
/// </summary>
public sealed class AuditLedgerEventFieldLockTests
{
    private static readonly string[] Expected = ["Action", "Item", "Outcome", "Summary", "TenantHash"];

    /// <summary>Verifies the public property set is exactly the closed field set, and the action is the fixed constant.</summary>
    [Fact]
    public void PublicProperties_AreExactlyTheClosedFieldSet()
    {
        var names = typeof(AuditLedgerEvent)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(static property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        names.ShouldBe(Expected.Order(StringComparer.Ordinal).ToArray());
        AuditLedgerEvent.ActionValue.ShouldBe("autonogrammer.audit.v1");
        AuditLedgerEvent.Kind.ShouldBe("note");

        var audit = new AuditLedgerEvent
        {
            Item = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            Outcome = "accepted",
            Summary = "audit",
            TenantHash = new string('a', 32),
        };
        audit.Action.ShouldBe(AuditLedgerEvent.ActionValue);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(audit, AuditJsonContext.Default.AuditLedgerEvent));
        document.RootElement.EnumerateObject().Select(static property => property.Name).Order(StringComparer.Ordinal).ShouldBe(["action", "item", "outcome", "summary", "tenantHash"]);
    }
}
