using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Remote;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>The New session picker with container and WSL environments: ordering, availability, validation from the last test, and the target that starts.</summary>
public sealed class NewSessionTargetContainerTests
{
    private static EnvironmentProfile Ssh(string id) => new()
    {
        Id = id,
        DisplayName = "Box " + id,
        Kind = EnvironmentKind.Ssh,
        Ssh = new SshProfile { Alias = "alias-" + id, RemoteWorkspaceRoot = "/srv/app" },
    };

    private static EnvironmentProfile Container(string id, string name = "devbox") => new()
    {
        Id = id,
        DisplayName = "Dev " + id,
        Kind = EnvironmentKind.Container,
        Container = new ContainerProfile { Container = name, WorkspaceFolder = "/workspaces/app" },
    };

    private static EnvironmentProfile Wsl(string id) => new()
    {
        Id = id,
        DisplayName = "Ubuntu " + id,
        Kind = EnvironmentKind.Wsl,
        Wsl = new WslProfile { Distro = "Ubuntu-24.04", WorkspacePath = "/home/dev/app" },
    };

    /// <summary>The options are this computer, ssh, container, WSL, then the cloud; WSL is listed only when the head says WSL exists.</summary>
    [Fact]
    public void TheOptionsAreLocalSshContainerWslCloud()
    {
        var model = new NewSessionTargetModel([Wsl("w"), Container("c"), Ssh("s")]) { IncludeWsl = true };

        model.Options.Select(option => option.Id).ShouldBe(["local", "ssh:s", "container:c", "wsl:w", "cloud"]);
        model.Options.Select(option => option.Kind).ShouldBe([SessionTargetKind.Local, SessionTargetKind.Ssh, SessionTargetKind.Container, SessionTargetKind.Wsl, SessionTargetKind.Cloud]);
        model.Options[2].Label.ShouldBe("container: Dev c", Case.Sensitive);
        model.Options[2].Detail.ShouldBe("devbox Folder in the container: /workspaces/app", Case.Sensitive);
        model.Options[3].Detail.ShouldBe("Ubuntu-24.04 Folder in the distribution: /home/dev/app", Case.Sensitive);

        model.IncludeWsl = false;

        model.Options.Select(option => option.Id).ShouldBe(["local", "ssh:s", "container:c", "cloud"]);
    }

    /// <summary>Hiding WSL keeps the choice when its option still exists and falls back to this computer when it does not.</summary>
    [Fact]
    public void HidingWslFallsBackToLocal()
    {
        var model = new NewSessionTargetModel([Container("c"), Wsl("w")]) { IncludeWsl = true };
        model.Select("wsl:w");
        model.Selected.ShouldNotBeNull().Kind.ShouldBe(SessionTargetKind.Wsl);

        model.IncludeWsl = false;

        model.Selected.ShouldNotBeNull().Id.ShouldBe("local", Case.Sensitive);
    }

    /// <summary>A container that was never tested can start (starting connects to it); the target carries the profile and the folder inside the container.</summary>
    [Fact]
    public void AnUntestedContainerCanStart()
    {
        var model = new NewSessionTargetModel([Container("c")]);
        model.Select("container:c");

        model.CanStart.ShouldBeTrue();
        model.Validation.Message.ShouldBe(RemoteCopy.TargetUntested, Case.Sensitive);
        model.TryGetTarget(out var target).ShouldBeTrue();
        target.ShouldNotBeNull();
        target.Kind.ShouldBe(SessionTargetKind.Container);
        target.ProfileId.ShouldBe("c", Case.Sensitive);
        target.RemoteWorkspaceRoot.ShouldBe("/workspaces/app", Case.Sensitive);
    }

    /// <summary>A WSL target carries the workspace path in the distribution.</summary>
    [Fact]
    public void AWslTargetCarriesTheWorkspacePath()
    {
        var model = new NewSessionTargetModel([Wsl("w")]) { IncludeWsl = true };
        model.Select("wsl:w");

        model.TryGetTarget(out var target).ShouldBeTrue();

        target.ShouldNotBeNull().RemoteWorkspaceRoot.ShouldBe("/home/dev/app", Case.Sensitive);
        target.Kind.ShouldBe(SessionTargetKind.Wsl);
    }

    /// <summary>The failed outcomes of a test block the start and say why in words; a good outcome allows it.</summary>
    [Theory]
    [InlineData(RemoteEnvironmentStatus.NotRunning, false, "Container not running")]
    [InlineData(RemoteEnvironmentStatus.NotFound, false, "Not found")]
    [InlineData(RemoteEnvironmentStatus.RuntimeUnavailable, false, "Runtime not available")]
    [InlineData(RemoteEnvironmentStatus.VersionMismatch, false, "omp missing or too old")]
    [InlineData(RemoteEnvironmentStatus.Ready, true, "Ready")]
    [InlineData(RemoteEnvironmentStatus.ReadyWithWarnings, true, "Ready with warnings")]
    public void TheLastTestDecidesWhetherItCanStart(RemoteEnvironmentStatus status, bool canStart, string words)
    {
        var model = new NewSessionTargetModel([Container("c")]);
        model.Select("container:c");

        model.SetStatus("c", status);

        model.CanStart.ShouldBe(canStart);
        model.TryGetTarget(out _).ShouldBe(canStart);
        model.Selected.ShouldNotBeNull().StatusLabel.ShouldBe(words, Case.Sensitive);
        if (!canStart)
        {
            model.Validation.Message.ShouldStartWith(RemoteCopy.TargetBlockedPrefix, Case.Sensitive);
            model.Validation.Message.ShouldContain(words, Case.Sensitive);
        }
    }

    /// <summary>An environment whose profile is invalid cannot start, and the message names the field to fix.</summary>
    [Fact]
    public void AnInvalidContainerProfileCannotStart()
    {
        var bad = Container("c") with { Container = new ContainerProfile { Container = "--rm", WorkspaceFolder = "/w" } };
        var model = new NewSessionTargetModel([bad]);
        model.Select("container:c");

        model.CanStart.ShouldBeFalse();
        model.Validation.Message.ShouldStartWith(RemoteCopy.TargetBlockedPrefix, Case.Sensitive);
        model.Validation.Message.ShouldContain("Remote environments", Case.Sensitive);
    }

    /// <summary>The status of a container persists across a profile refresh and shows in the accessible name.</summary>
    [Fact]
    public void TheStatusPersistsAcrossARefresh()
    {
        var model = new NewSessionTargetModel([Container("c")]);
        model.SetStatus("c", RemoteEnvironmentStatus.NotRunning);

        model.SetProfiles([Container("c"), Container("d", "other")]);

        model.Options.Single(option => option.Id == "container:c").StatusLabel.ShouldBe("Container not running", Case.Sensitive);
        model.Options.Single(option => option.Id == "container:c").AccessibleName.ShouldContain("Container not running", Case.Sensitive);
        model.Options.Single(option => option.Id == "container:d").StatusLabel.ShouldBeEmpty();
    }

    /// <summary>A container profile without its container details, and unknown kinds, are left out of the picker.</summary>
    [Fact]
    public void BrokenAndUnknownProfilesAreLeftOut()
    {
        var withoutDetails = new EnvironmentProfile { Id = "x", DisplayName = "X", Kind = EnvironmentKind.Container };
        var unknown = new EnvironmentProfile { Id = "y", DisplayName = "Y", Kind = (EnvironmentKind)42 };

        var model = new NewSessionTargetModel([withoutDetails, unknown, Container("c")]);

        model.Options.Select(option => option.Id).ShouldBe(["local", "container:c", "cloud"]);
    }
}
