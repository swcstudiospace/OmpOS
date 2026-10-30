using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Tools;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Tools;

/// <summary>Owner-only grants and the hash-chained audit.</summary>
public sealed class CoworkAuditGrantStoreTests
{
    [Fact]
    public async Task TamperedAuditPrefix_IsDetected()
    {
        var root = Path.Combine(Path.GetTempPath(), "cowork-audit-" + Guid.NewGuid().ToString("N"));
        using var audit = new FileCoworkToolAudit(root, "acct");
        await audit.RecordAsync(Entry("one"), TestContext.Current.CancellationToken);
        await audit.RecordAsync(Entry("two"), TestContext.Current.CancellationToken);
        var file = Directory.GetFiles(audit.DirectoryPath, "*.jsonl").ShouldHaveSingleItem();
        FileCoworkToolAudit.VerifyChain(file).ShouldBeTrue();
        var lines = await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken);
        lines[0] = lines[0].Replace("artifact_read", "artifact_xxxx", StringComparison.Ordinal);
        await File.WriteAllLinesAsync(file, lines, TestContext.Current.CancellationToken);
        FileCoworkToolAudit.VerifyChain(file).ShouldBeFalse();
    }

    [Fact]
    public async Task AuditWriteFailure_DoesNotEscape()
    {
        var blocked = Path.Combine(Path.GetTempPath(), "cowork-audit-block-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(blocked, "not-a-directory", TestContext.Current.CancellationToken);
        using var audit = new FileCoworkToolAudit(blocked, "acct");
        await Should.NotThrowAsync(() => audit.RecordAsync(Entry("one"), TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task GrantStore_RoundTripsOwnerOnlyJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "cowork-grants-" + Guid.NewGuid().ToString("N"));
        using var store = new FileCoworkGrantStore(root, "acct");
        var key = new CoworkGrantKey("artifact.write", "cowork");
        (await store.IsGrantedAsync(key, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await store.GrantAsync(key, TestContext.Current.CancellationToken);
        (await store.IsGrantedAsync(key, TestContext.Current.CancellationToken)).ShouldBeTrue();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(store.FilePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        await store.RevokeAsync(key, TestContext.Current.CancellationToken);
        (await store.IsGrantedAsync(key, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    private static CoworkToolAuditEntry Entry(string callId) =>
        new("s", callId, "artifact", "artifact_read", CoworkTurnOrigin.Local, CoworkPolicyOutcome.Allow, null, false, CoworkResultKinds.Text, TimeSpan.FromMilliseconds(3), "target");
}
