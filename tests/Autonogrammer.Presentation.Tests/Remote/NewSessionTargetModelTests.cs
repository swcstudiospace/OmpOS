using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The environment selector of the New session dialog: the options, what can start, and the sentence that says why a choice cannot.</summary>
public sealed class NewSessionTargetModelTests
{
    private static EnvironmentProfile Ssh(string id, string root = "/srv/app") => new()
    {
        Id = id,
        DisplayName = "Box " + id,
        Kind = EnvironmentKind.Ssh,
        Ssh = new SshProfile { Alias = "alias-" + id, RemoteWorkspaceRoot = root },
    };

    /// <summary>The options are this computer, each ssh environment in order, and the cloud last; this computer is chosen and can start.</summary>
    [Fact]
    public void TheOptionsAreLocalThenSshThenCloud()
    {
        var model = new NewSessionTargetModel([Ssh("a"), Ssh("b")]);

        model.Options.Select(option => option.Id).ShouldBe(["local", "ssh:a", "ssh:b", "cloud"]);
        model.Options.Select(option => option.Kind).ShouldBe([SessionTargetKind.Local, SessionTargetKind.Ssh, SessionTargetKind.Ssh, SessionTargetKind.Cloud]);
        model.Selected.ShouldBeSameAs(model.Options[0]);
        model.CanStart.ShouldBeTrue();
        model.Validation.Message.ShouldBe(RemoteCopy.TargetOk);
        model.TryGetTarget(out var target).ShouldBeTrue();
        target!.Kind.ShouldBe(SessionTargetKind.Local);
        target.ProfileId.ShouldBeNull();
        target.RemoteWorkspaceRoot.ShouldBeNull();
    }

    /// <summary>Only ssh environments are listed; other kinds and environments without ssh details are left out.</summary>
    [Fact]
    public void OnlySshEnvironmentsAreListed()
    {
        var model = new NewSessionTargetModel(
        [
            Ssh("a"),
            new EnvironmentProfile { Id = "c", DisplayName = "Cloud", Kind = EnvironmentKind.Cloud },
            new EnvironmentProfile { Id = "l", DisplayName = "Local", Kind = EnvironmentKind.Local },
        ]);

        model.Options.Select(option => option.Id).ShouldBe(["local", "ssh:a", "cloud"]);
    }

    /// <summary>The cloud is listed but cannot be chosen to start; the message says why.</summary>
    [Fact]
    public void TheCloudIsListedButCannotStart()
    {
        var model = new NewSessionTargetModel([]);

        model.Select("cloud");

        model.Selected!.IsAvailable.ShouldBeFalse();
        model.CanStart.ShouldBeFalse();
        model.Validation.Message.ShouldBe(RemoteCopy.CloudUnavailable);
        model.TryGetTarget(out var target).ShouldBeFalse();
        target.ShouldBeNull();
    }

    /// <summary>An ssh environment that was never tested can start and the message says that starting connects; its target carries the profile and the remote folder.</summary>
    [Fact]
    public void AnUntestedSshEnvironmentCanStart()
    {
        var model = new NewSessionTargetModel([Ssh("a", "/srv/api")]);

        model.Select("ssh:a");

        model.CanStart.ShouldBeTrue();
        model.Validation.Message.ShouldBe(RemoteCopy.TargetUntested);
        model.TryGetTarget(out var target).ShouldBeTrue();
        target!.Kind.ShouldBe(SessionTargetKind.Ssh);
        target.ProfileId.ShouldBe("a");
        target.RemoteWorkspaceRoot.ShouldBe("/srv/api");
        target.Label.ShouldBe(RemoteCopy.SshPrefix + "Box a");
    }

    /// <summary>A failed test blocks starting on that environment and the message names the status and what to do; a later passing test unblocks it.</summary>
    [Theory]
    [InlineData(RemoteEnvironmentStatus.HostKeyUnconfirmed)]
    [InlineData(RemoteEnvironmentStatus.HostKeyChanged)]
    [InlineData(RemoteEnvironmentStatus.AuthFailed)]
    [InlineData(RemoteEnvironmentStatus.Unreachable)]
    [InlineData(RemoteEnvironmentStatus.VersionMismatch)]
    [InlineData(RemoteEnvironmentStatus.NeedsSetup)]
    public void AFailedTestBlocksStarting(RemoteEnvironmentStatus status)
    {
        var model = new NewSessionTargetModel([Ssh("a")]);
        model.Select("ssh:a");

        model.SetStatus("a", status);

        model.CanStart.ShouldBeFalse();
        model.Validation.Message.ShouldStartWith(RemoteCopy.TargetBlockedPrefix, Case.Sensitive);
        model.Validation.Message.ShouldContain(RemoteStatusDescriber.LabelOf(status), Case.Sensitive);
        model.Options[1].StatusLabel.ShouldBe(RemoteStatusDescriber.LabelOf(status));
        model.TryGetTarget(out _).ShouldBeFalse();

        model.SetStatus("a", RemoteEnvironmentStatus.Ready);

        model.CanStart.ShouldBeTrue();
        model.Validation.Message.ShouldBe(RemoteCopy.TargetOk);
    }

    /// <summary>A connected, ready or reconnecting host can start; a host that was not checked says so.</summary>
    [Theory]
    [InlineData(RemoteEnvironmentStatus.Ready)]
    [InlineData(RemoteEnvironmentStatus.ReadyWithWarnings)]
    [InlineData(RemoteEnvironmentStatus.Connected)]
    [InlineData(RemoteEnvironmentStatus.Reconnecting)]
    [InlineData(RemoteEnvironmentStatus.NotChecked)]
    [InlineData(RemoteEnvironmentStatus.Disconnected)]
    public void WorkingStatesCanStart(RemoteEnvironmentStatus status)
    {
        var model = new NewSessionTargetModel([Ssh("a")]);
        model.Select("ssh:a");

        model.SetStatus("a", status);

        model.CanStart.ShouldBeTrue();
        string.Equals(model.Validation.Message, RemoteCopy.TargetUntested, StringComparison.Ordinal).ShouldBe(status == RemoteEnvironmentStatus.NotChecked);
    }

    /// <summary>An ssh environment whose stored profile no longer passes the validator cannot start, and the message points to the settings page without echoing the value.</summary>
    [Fact]
    public void AnInvalidProfileCannotStart()
    {
        var bad = new EnvironmentProfile
        {
            Id = "bad",
            DisplayName = "Bad",
            Kind = EnvironmentKind.Ssh,
            Ssh = new SshProfile { Alias = "-oProxyCommand=evil", RemoteWorkspaceRoot = "/w" },
        };
        var model = new NewSessionTargetModel([bad]);

        model.Select("ssh:bad");

        model.CanStart.ShouldBeFalse();
        model.Validation.Message.ShouldContain("Remote environments", Case.Sensitive);
        model.Validation.Message.ShouldNotContain("evil", Case.Sensitive);
    }

    /// <summary>Selecting an unknown option is refused; replacing the profiles keeps the choice when its option remains and falls back to this computer when it does not; statuses survive.</summary>
    [Fact]
    public void ReplacingTheProfilesKeepsOrResetsTheChoice()
    {
        var model = new NewSessionTargetModel([Ssh("a"), Ssh("b")]);
        Should.Throw<ArgumentException>(() => model.Select("ssh:zzz"));
        model.Select("ssh:b");
        model.SetStatus("b", RemoteEnvironmentStatus.Unreachable);
        var notified = new List<string?>();
        model.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        model.SetProfiles([Ssh("b"), Ssh("c")]);

        model.Selected!.Id.ShouldBe("ssh:b");
        model.Selected.Status.ShouldBe(RemoteEnvironmentStatus.Unreachable);
        model.CanStart.ShouldBeFalse();

        model.SetProfiles([Ssh("c")]);

        model.Selected!.Id.ShouldBe("local");
        model.CanStart.ShouldBeTrue();
        notified.ShouldContain("Selected", StringComparer.Ordinal);
        notified.ShouldContain("Validation", StringComparer.Ordinal);
        notified.ShouldContain("CanStart", StringComparer.Ordinal);
    }

    /// <summary>The accessible name of a row says its name, where it points, its status and why it is unavailable.</summary>
    [Fact]
    public void TheAccessibleNameSaysEverythingTheRowShows()
    {
        var model = new NewSessionTargetModel([Ssh("a")]);
        model.SetStatus("a", RemoteEnvironmentStatus.Ready);

        model.Options[1].AccessibleName.ShouldBe("ssh: Box a. alias-a Folder on the host: /srv/app. Ready");
        model.Options[2].AccessibleName.ShouldContain(RemoteCopy.CloudUnavailable, Case.Sensitive);
        model.Options[0].AccessibleName.ShouldBe(RemoteCopy.LocalLabel + ". " + RemoteCopy.LocalDetail);
    }

    /// <summary>Null arguments are refused.</summary>
    [Fact]
    public void NullArgumentsAreRefused()
    {
        Should.Throw<ArgumentNullException>(() => new NewSessionTargetModel(null!));
        Should.Throw<ArgumentNullException>(() => new NewSessionTargetModel([]).SetStatus(null!, RemoteEnvironmentStatus.Ready));
        Should.Throw<ArgumentNullException>(() => new NewSessionTargetModel([]).Select(null!));
    }
}
