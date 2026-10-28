using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Remote;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>The container and WSL forms: live problems per field, the identifier made from the name, fields that are blank meaning "not set", and no field for a secret or another runtime.</summary>
public sealed class ContainerProfileEditorTests
{
    private static HashSet<string> Taken(params string[] ids) => new HashSet<string>(ids, StringComparer.Ordinal);

    /// <summary>An empty new container form shows a problem on the name and the folder, and builds nothing.</summary>
    [Fact]
    public void AnEmptyContainerFormIsInvalid()
    {
        var editor = ContainerProfileEditor.ForNew(Taken());

        editor.IsNew.ShouldBeTrue();
        editor.Heading.ShouldBe("Add a container");
        editor.IsValid.ShouldBeFalse();
        editor.Issues.Select(issue => issue.Field).ShouldBe(["Container", "DisplayName", "WorkspaceFolder"], ignoreOrder: true);
        editor.TryBuild(out var profile).ShouldBeFalse();
        profile.ShouldBeNull();
    }

    /// <summary>Filling the fields clears the problems live, and the built profile trims the text and leaves blank optional fields unset.</summary>
    [Fact]
    public void FillingTheFormBuildsTheProfile()
    {
        var editor = ContainerProfileEditor.ForNew(Taken());

        editor.Container = " devbox ";
        editor.DisplayName = " Dev box ";
        editor.WorkspaceFolder = " /workspaces/my app ";
        editor.RemoteUser = "   ";
        editor.PodmanPath = string.Empty;
        editor.OmpPath = "/opt/omp/bin/omp";

        editor.IsValid.ShouldBeTrue();
        editor.Issues.ShouldBeEmpty();
        editor.TryBuild(out var profile).ShouldBeTrue();
        profile.ShouldNotBeNull();
        profile.Id.ShouldBe("container-devbox");
        profile.DisplayName.ShouldBe("Dev box");
        profile.Kind.ShouldBe(EnvironmentKind.Container);
        profile.Container.ShouldBe(new ContainerProfile { Container = "devbox", WorkspaceFolder = "/workspaces/my app", OmpPath = "/opt/omp/bin/omp" });
        EnvironmentProfileValidator.Validate(profile).ShouldBeEmpty();
    }

    /// <summary>A hostile value is a problem on its own field, with the rule and never the value.</summary>
    [Fact]
    public void HostileValuesAreProblemsOnTheirField()
    {
        var editor = ContainerProfileEditor.ForNew(Taken(), "devbox", "/workspaces/app");

        editor.Container = "--privileged";
        editor.RemoteUser = "a b";
        editor.WorkspaceFolder = "relative";
        editor.PodmanPath = "-x";
        editor.OmpPath = "../omp";

        editor.IsValid.ShouldBeFalse();
        editor.Issues.Select(issue => issue.Field).ShouldBe(["Container", "RemoteUser", "WorkspaceFolder", "PodmanPath", "OmpPath"], ignoreOrder: true);
        editor.Issues.ShouldAllBe(issue => !issue.Message.Contains("--privileged", StringComparison.Ordinal));
    }

    /// <summary>The new identifier is not one that is taken: a numeric suffix is added.</summary>
    [Fact]
    public void TheIdentifierIsUnique()
    {
        var editor = ContainerProfileEditor.ForNew(Taken("container-devbox", "container-devbox-2"), "devbox", "/w");

        editor.TryBuild(out var profile).ShouldBeTrue();

        profile.ShouldNotBeNull().Id.ShouldBe("container-devbox-3");
    }

    /// <summary>Editing keeps the identifier and shows the stored values; changing a value shows a problem live.</summary>
    [Fact]
    public void EditingKeepsTheIdentifier()
    {
        var existing = new EnvironmentProfile
        {
            Id = "keep-this-id",
            DisplayName = "Dev box",
            Kind = EnvironmentKind.Container,
            Container = new ContainerProfile { Container = "devbox", RemoteUser = "dev", WorkspaceFolder = "/workspaces/app", PodmanPath = "/usr/bin/podman" },
        };

        var editor = ContainerProfileEditor.ForEdit(existing);
        editor.Container = "renamed";

        editor.IsNew.ShouldBeFalse();
        editor.Heading.ShouldBe("Edit a container");
        editor.RemoteUser.ShouldBe("dev");
        editor.PodmanPath.ShouldBe("/usr/bin/podman");
        editor.TryBuild(out var profile).ShouldBeTrue();
        profile.ShouldNotBeNull().Id.ShouldBe("keep-this-id");
        profile.Container.ShouldNotBeNull().Container.ShouldBe("renamed");
        editor.Container = "-bad";
        editor.IsValid.ShouldBeFalse();
    }

    /// <summary>Only a container environment can be edited in the container form, and only a WSL one in the WSL form.</summary>
    [Fact]
    public void TheWrongKindIsRefused()
    {
        var ssh = new EnvironmentProfile { Id = "s", DisplayName = "S", Kind = EnvironmentKind.Ssh, Ssh = new SshProfile { Alias = "a", RemoteWorkspaceRoot = "/w" } };

        Should.Throw<ArgumentException>(() => ContainerProfileEditor.ForEdit(ssh));
        Should.Throw<ArgumentException>(() => WslProfileEditor.ForEdit(ssh));
        Should.Throw<ArgumentNullException>(() => ContainerProfileEditor.ForNew(null!));
    }

    /// <summary>The WSL form builds a valid profile, shows problems per field, and derives a unique identifier.</summary>
    [Fact]
    public void TheWslFormBuildsAndValidates()
    {
        var editor = WslProfileEditor.ForNew(Taken("wsl-ubuntu-24.04"), "Ubuntu-24.04");
        editor.Heading.ShouldBe("Add a WSL distribution");
        editor.IsValid.ShouldBeFalse();
        editor.Issues.ShouldHaveSingleItem().Field.ShouldBe("WorkspacePath");

        editor.WorkspacePath = "/home/dev/app";
        editor.User = "dev";
        editor.OmpPath = "omp";

        editor.TryBuild(out var profile).ShouldBeTrue();
        profile.ShouldNotBeNull();
        profile.Id.ShouldBe("wsl-ubuntu-24.04-2");
        profile.Kind.ShouldBe(EnvironmentKind.Wsl);
        profile.Wsl.ShouldBe(new WslProfile { Distro = "Ubuntu-24.04", User = "dev", WorkspacePath = "/home/dev/app", OmpPath = "omp" });

        editor.Distro = "a b";
        editor.User = "x y";
        editor.Issues.Select(issue => issue.Field).ShouldBe(["Distro", "User"], ignoreOrder: true);
    }

    /// <summary>The WSL form edits an existing environment and keeps its identifier.</summary>
    [Fact]
    public void TheWslFormEditsAnExistingEnvironment()
    {
        var existing = new EnvironmentProfile { Id = "wsl-1", DisplayName = "Ubuntu", Kind = EnvironmentKind.Wsl, Wsl = new WslProfile { Distro = "Debian", WorkspacePath = "/w" } };

        var editor = WslProfileEditor.ForEdit(existing);

        editor.IsNew.ShouldBeFalse();
        editor.Heading.ShouldBe("Edit a WSL distribution");
        editor.TryBuild(out var profile).ShouldBeTrue();
        profile.ShouldNotBeNull().Id.ShouldBe("wsl-1");
    }
}
