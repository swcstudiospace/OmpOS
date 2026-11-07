using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.AgentLoop.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests;

/// <summary>The Cowork registry is a separate file with its own cap and recovery.</summary>
public sealed class CoworkSessionRegistryTests
{
    [Fact]
    public void AFullRegistryRefusesWithTheCoworkMessage()
    {
        using var directory = new TempState();
        var registry = Open(directory.Path, "subject-full");
        for (var index = 0; index < AgentSessionRegistry.MaxSessions; index++)
        {
            registry.Create("task");
        }

        var exception = Should.Throw<CoworkSessionLimitException>(() => registry.Create("one more"));
        exception.Message.ShouldBe("You have 200 Cowork tasks. Delete or archive one to start another.");
        registry.List().Count.ShouldBe(AgentSessionRegistry.MaxSessions);
    }

    [Fact]
    public void RecoverAfterRestartTouchesOnlyCoworkRows()
    {
        using var directory = new TempState();
        var codePath = Path.Combine(directory.Path, "sessions.json");
        var code = new AgentSessionRegistry(codePath, new FixedClock(Stamp), options: new AgentSessionRegistryOptions { StateRoot = directory.Path });
        var codeId = code.Create("code", null).Id;
        code.SetLifecycle(codeId, SessionLifecycle.Running);
        var codeBytes = File.ReadAllBytes(codePath);

        var cowork = Open(directory.Path, "subject-recover");
        var running = cowork.Create("running").Id;
        var paused = cowork.Create("paused").Id;
        cowork.SetStatus(running, CoworkSessionStatus.Running);
        cowork.SetStatus(paused, CoworkSessionStatus.Paused);

        var lost = cowork.RecoverAfterRestart();
        lost.ShouldBe([running]);
        cowork.List().Single(row => string.Equals(row.Id, running, StringComparison.Ordinal)).Status.ShouldBe(CoworkSessionStatus.Lost);
        cowork.List().Single(row => string.Equals(row.Id, paused, StringComparison.Ordinal)).Status.ShouldBe(CoworkSessionStatus.Paused);
        code.TryGet(codeId, out var codeRecord).ShouldBeTrue();
        codeRecord!.Lifecycle.ShouldBe(SessionLifecycle.Running);
        File.ReadAllBytes(codePath).ShouldBe(codeBytes);
        cowork.StateFilePath.ShouldEndWith(Path.Combine("cowork", CoworkAccountFolder.KeyFromSubject("subject-recover"), "sessions.json"));
    }

    [Fact]
    public void ACorruptFileIsQuarantinedAndNotOverwritten()
    {
        using var directory = new TempState();
        var key = CoworkAccountFolder.KeyFromSubject("subject-corrupt");
        var account = CoworkAccountFolder.AccountDirectory(directory.Path, key);
        Directory.CreateDirectory(account);
        var corrupt = "{ this is not json"u8.ToArray();
        var path = Path.Combine(account, "sessions.json");
        File.WriteAllBytes(path, corrupt);

        var registry = CoworkSessionRegistry.Open(
            new CoworkSessionOptions { StateRoot = directory.Path },
            new FakeGate(key),
            new FixedClock(Stamp));

        registry.List().ShouldBeEmpty();
        File.Exists(path).ShouldBeFalse();
        var kept = Directory.GetFiles(account, "sessions.json.quarantined*");
        kept.Length.ShouldBe(1);
        registry.ReadBanner.ShouldBe("Cowork tasks could not be read; the file was kept as " + Path.GetFileName(kept[0]));
        File.ReadAllBytes(kept[0]).ShouldBe(corrupt);
    }

    private static readonly DateTimeOffset Stamp = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private static CoworkSessionRegistry Open(string root, string subject) =>
        CoworkSessionRegistry.Open(
            new CoworkSessionOptions { StateRoot = root },
            new FakeGate(CoworkAccountFolder.KeyFromSubject(subject)),
            new FixedClock(Stamp));

    private sealed class FakeGate(string key) : ICoworkAccountGate
    {
        public bool IsSignedIn => true;

        public string? AccountFolderKey => key;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TempState : IDisposable
    {
        public TempState()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cowork-reg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
