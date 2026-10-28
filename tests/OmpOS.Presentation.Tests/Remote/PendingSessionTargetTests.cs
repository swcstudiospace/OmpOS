using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Remote;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>What the next new session starts on: taken once, cleared by taking, and never a local target.</summary>
public sealed class PendingSessionTargetTests
{
    private static SessionTarget Ssh() => new(
        SessionTargetKind.Ssh,
        "ssh: box",
        new EnvironmentProfile { Id = "box", DisplayName = "box", Kind = EnvironmentKind.Ssh, Ssh = new SshProfile { Alias = "alpha", RemoteWorkspaceRoot = "/srv/app" } });

    /// <summary>A target is taken once: the second take finds nothing, so only the session created next starts there.</summary>
    [Fact]
    public void ATargetIsTakenOnce()
    {
        var pending = new PendingSessionTarget();
        pending.Set(Ssh());

        pending.Take().ShouldNotBeNull().ProfileId.ShouldBe("box", Case.Sensitive);
        pending.Take().ShouldBeNull();
    }

    /// <summary>Setting a local target, or null, clears what was pending.</summary>
    [Fact]
    public void ALocalTargetClearsThePendingOne()
    {
        var pending = new PendingSessionTarget();
        pending.Set(Ssh());
        pending.Set(new SessionTarget(SessionTargetKind.Local, "This computer", null));

        pending.Take().ShouldBeNull();

        pending.Set(Ssh());
        pending.Set(null);
        pending.Take().ShouldBeNull();
    }

    /// <summary>The folders of a workspace set are taken once, in order, and an empty list clears them.</summary>
    [Fact]
    public void FoldersAreTakenOnceInOrder()
    {
        var pending = new PendingSessionTarget();
        pending.SetRoots([new WorkspacePickerRoot("app", "/work/app", "app", null, IsPrimary: true), new WorkspacePickerRoot("lib", "/work/lib", "lib", null, IsPrimary: false)]);

        pending.TakeRoots().ShouldNotBeNull().Select(static root => root.Path).ShouldBe(["/work/app", "/work/lib"]);
        pending.TakeRoots().ShouldBeNull();

        pending.SetRoots([new WorkspacePickerRoot("app", "/work/app", "app", null, IsPrimary: true)]);
        pending.SetRoots([]);
        pending.TakeRoots().ShouldBeNull();
    }

    /// <summary>The target and the folders are independent: taking one leaves the other.</summary>
    [Fact]
    public void TheTargetAndTheFoldersAreIndependent()
    {
        var pending = new PendingSessionTarget();
        pending.Set(Ssh());
        pending.SetRoots([new WorkspacePickerRoot("app", "/srv/app", "app", "box", IsPrimary: true)]);

        pending.Take().ShouldNotBeNull();
        pending.TakeRoots().ShouldNotBeNull();
    }
}
