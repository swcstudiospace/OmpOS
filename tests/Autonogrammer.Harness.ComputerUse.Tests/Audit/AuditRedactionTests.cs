using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.ComputerUse.Audit;
using Autonogrammer.Harness.ComputerUse.Tests.Policy;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Audit;

/// <summary>Typed text and clipboard bytes never land in the log or a ledger.</summary>
public sealed class AuditRedactionTests
{
    [Fact]
    public async Task TypedTextIsLengthAndHashClipboardNeverAppearsAndLedgerStaysEmpty()
    {
        string root = Directory.CreateTempSubdirectory("cu-redact").FullName;
        ManualTimeProvider time = new(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero));
        ComputerAuditLog log = new(root, time, thumbnails: false);
        LedgerSpy ledger = new();
        const string typed = "super-secret-typed-value";
        const string clipboard = "clipboard-secret-bytes-never";
        (int length, string hash) = ComputerAuditLog.HashTypedText(typed);
        string title = new('t', 90);
        log.TryAppend(new ComputerAuditWrite
        {
            SessionId = "s",
            Tool = "computer_type",
            Summary = "type",
            AppIdentity = "app",
            Title = title,
            Decision = "allowed",
            ApprovalOrigin = "local",
            Outcome = "after",
            TypedLength = length,
            TypedSha256 = hash,
        }).ShouldBeTrue();
        log.TryAppend(new ComputerAuditWrite
        {
            SessionId = "s",
            Tool = "computer_clipboard_read",
            Summary = "clipboard-read",
            AppIdentity = "app",
            Title = "clip",
            Decision = "allowed",
            ApprovalOrigin = "local",
            Outcome = "after",
        }).ShouldBeTrue();
        CoworkToolAuditSinkAdapter adapter = new(log);
        await adapter.RecordAsync(
            new CoworkToolAuditEntry("s", "c", "computer", "computer_click", CoworkTurnOrigin.Local, CoworkPolicyOutcome.Allow, null, false, CoworkResultKinds.Text, TimeSpan.Zero, "app"),
            CancellationToken.None);
        string file = await File.ReadAllTextAsync(Path.Combine(root, "computer-use", "audit", "2026-03-02.jsonl"), TestContext.Current.CancellationToken);
        file.Contains(typed, StringComparison.Ordinal).ShouldBeFalse();
        file.Contains(clipboard, StringComparison.Ordinal).ShouldBeFalse();
        file.Contains(hash, StringComparison.Ordinal).ShouldBeTrue();
        file.Contains("\"typedLength\":" + length, StringComparison.Ordinal).ShouldBeTrue();
        file.Contains(title, StringComparison.Ordinal).ShouldBeFalse();
        file.Contains(title[..80], StringComparison.Ordinal).ShouldBeTrue();
        file.Contains("cowork_events", StringComparison.Ordinal).ShouldBeFalse();
        ledger.Rows.ShouldBe(0);
    }

    private sealed class LedgerSpy
    {
        public int Rows { get; }
    }
}
