using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Computer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Computer;

/// <summary>The settings page's denylist addition: a failed add is said on the page and the typed identity stays for another try.</summary>
public sealed class ComputerSettingsViewModelTests
{
    /// <summary>A denylist store that cannot save reports the failure, and the identity the user typed is kept.</summary>
    [Fact]
    public void AFailedDenylistAdditionIsSaidAndTheIdentityIsKept()
    {
        var model = new ComputerSettingsViewModel(new NoGrants(), new RefusingDenylist(), new NoAudit(), new MemorySettings(), new IdleSwitch());
        model.DenylistIdentity = "  com.example.notes ";

        model.AddDenylist.Execute(null);

        model.DenylistFailure.ShouldBe("Could not add the denylist entry: disk is full.");
        model.DenylistIdentity.ShouldBe("  com.example.notes ");
    }

    private sealed class NoGrants : IComputerGrantRegistry
    {
        public IReadOnlyList<ComputerGrantView> Snapshot() => [];

        public void Revoke(string grantId)
        {
        }
    }

    private sealed class RefusingDenylist : IComputerDenylistStore
    {
        public IReadOnlyList<ComputerDenylistEntry> BuiltInRows => [];

        public ValueTask AddAsync(string appIdentity, CancellationToken cancellationToken) =>
            ValueTask.FromException(new IOException("disk is full."));
    }

    private sealed class NoAudit : IComputerAuditReader
    {
        public ValueTask<IReadOnlyList<AuditRecordView>> ReadAsync(int days, bool includeThumbnails, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<AuditRecordView>>([]);
    }

    private sealed class MemorySettings : IComputerUseSettingsStore
    {
        public ValueTask<ComputerUseUserSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(new ComputerUseUserSettings());

        public ValueTask SaveAsync(ComputerUseUserSettings settings, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class IdleSwitch : IKillSwitchControl
    {
        public KillSwitchState State => KillSwitchState.Armed;

        public event EventHandler<KillSwitchStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public ValueTask TripAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask RearmAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
