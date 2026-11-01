using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Audit;
using Autonogrammer.Harness.ComputerUse.Tests.Policy;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Audit;

/// <summary>Hash chain, write-ahead and denial when the directory cannot be written.</summary>
public sealed class AuditLogChainTests
{
    [Fact]
    public async Task ChainDetectsATruncatedPrefixAndWriteAheadDenies()
    {
        string root = Directory.CreateTempSubdirectory("cu-audit").FullName;
        ManualTimeProvider time = new(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        ComputerAuditLog log = new(root, time);
        ComputerAuditWrite write = Sample();
        int injected = 0;
        DenialCode? denial = await log.WriteAroundAsync(write, _ =>
        {
            injected++;
            return ValueTask.CompletedTask;
        }, CancellationToken.None);
        denial.ShouldBeNull();
        injected.ShouldBe(1);
        log.Verify().ShouldBeTrue();
        string path = Path.Combine(root, "computer-use", "audit", "2026-03-01.jsonl");
        string[] lines = await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken);
        lines.Length.ShouldBe(2);
        using var first = System.Text.Json.JsonDocument.Parse(lines[0]);
        first.RootElement.GetProperty("prev").GetString().ShouldBe(ComputerAuditLog.GenesisPrev);
        string hashed = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lines[0])));
        using var second = System.Text.Json.JsonDocument.Parse(lines[1]);
        second.RootElement.GetProperty("prev").GetString().ShouldBe(hashed);
        second.RootElement.GetProperty("outcome").GetString().ShouldBe("after");
        await File.WriteAllTextAsync(path, lines[0][..^4] + "\n" + lines[1] + "\n", TestContext.Current.CancellationToken);
        log.Verify().ShouldBeFalse();
        string held = Path.Combine(root, "computer-use", "audit", "2026-03-01.jsonl");
        using FileStream exclusive = new(held, FileMode.Open, FileAccess.Read, FileShare.None);
        bool wrote = log.TryAppend(write);
        wrote.ShouldBeFalse();
        ComputerAuditLog.DenialFor(wrote).ShouldBe(DenialCode.AuditUnavailable);
    }

    private static ComputerAuditWrite Sample() => new()
    {
        SessionId = "s",
        Tool = "computer_click",
        Summary = "click",
        AppIdentity = "app",
        Title = "title",
        Decision = "allowed",
        ApprovalOrigin = "local",
        Outcome = "pending",
    };
}
