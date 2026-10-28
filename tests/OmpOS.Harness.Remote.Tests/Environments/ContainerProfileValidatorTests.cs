using OmpOS.Contracts.Environments;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The rules that keep a hostile container name, user or path out of a podman argument list and a shell line inside the container or the distribution.</summary>
public sealed class ContainerProfileValidatorTests
{
    private static ContainerProfile Container() => new() { Container = "devbox", WorkspaceFolder = "/workspaces/app" };

    private static WslProfile Distro() => new() { Distro = "Ubuntu-24.04", WorkspacePath = "/home/dev/app" };

    /// <summary>Plain profiles pass with no issue.</summary>
    [Fact]
    public void ValidProfilesPass()
    {
        EnvironmentProfileValidator.Validate(Container()).ShouldBeEmpty();
        EnvironmentProfileValidator.Validate(Container() with { RemoteUser = "dev", PodmanPath = "/usr/bin/podman", OmpPath = "/opt/omp/bin/omp" }).ShouldBeEmpty();
        EnvironmentProfileValidator.Validate(Distro()).ShouldBeEmpty();
        EnvironmentProfileValidator.Validate(Distro() with { User = "dev", OmpPath = "omp" }).ShouldBeEmpty();
        EnvironmentProfileValidator.IsValid(Container()).ShouldBeTrue();
        EnvironmentProfileValidator.IsValid(Distro()).ShouldBeTrue();
    }

    /// <summary>A container reference that could be read as an option, split into words or expanded by a shell is refused; a name or id that podman accepts passes.</summary>
    [Theory]
    [InlineData("devbox", true)]
    [InlineData("my_container.v2-a", true)]
    [InlineData("f809eef36cd7bd269045df7d48fbf69c6b2c990d24acc3917a9e986315f3a22e", true)]
    [InlineData("", false)]
    [InlineData("-rm", false)]
    [InlineData("--privileged", false)]
    [InlineData("a b", false)]
    [InlineData("a;b", false)]
    [InlineData("a$(id)", false)]
    [InlineData("a`id`", false)]
    [InlineData("a'b", false)]
    [InlineData("a/b", false)]
    [InlineData("a\nb", false)]
    [InlineData("_leading", false)]
    public void ContainerReferencesAreChecked(string value, bool expected)
    {
        ContainerProfileRules.IsValidContainerReference(value).ShouldBe(expected);
        EnvironmentProfileValidator.Validate(Container() with { Container = value }).Any(issue => string.Equals(issue.Property, "Container.Container", StringComparison.Ordinal)).ShouldBe(!expected);
    }

    /// <summary>A reference longer than 128 characters is refused.</summary>
    [Fact]
    public void AnOverlongContainerReferenceIsRefused()
    {
        ContainerProfileRules.IsValidContainerReference(new string('a', 128)).ShouldBeTrue();
        ContainerProfileRules.IsValidContainerReference(new string('a', 129)).ShouldBeFalse();
    }

    /// <summary>The user inside a container or distribution is a plain name or number; anything an option or a shell would read specially is refused.</summary>
    [Theory]
    [InlineData("dev", true)]
    [InlineData("1000", true)]
    [InlineData("root", true)]
    [InlineData("-u", false)]
    [InlineData("a b", false)]
    [InlineData("a:b", false)]
    [InlineData("a;b", false)]
    [InlineData("a\nb", false)]
    [InlineData("", false)]
    public void InnerUsersAreChecked(string value, bool expected)
    {
        EnvironmentProfileValidator.Validate(Container() with { RemoteUser = value }).Any(issue => string.Equals(issue.Property, "Container.RemoteUser", StringComparison.Ordinal)).ShouldBe(!expected);
        EnvironmentProfileValidator.Validate(Distro() with { User = value }).Any(issue => string.Equals(issue.Property, "Wsl.User", StringComparison.Ordinal)).ShouldBe(!expected);
    }

    /// <summary>A workspace folder is an absolute POSIX path inside the container, free of control characters and parent segments.</summary>
    [Theory]
    [InlineData("/workspaces/app", true)]
    [InlineData("/workspaces/my project", true)]
    [InlineData("/", true)]
    [InlineData("workspaces/app", false)]
    [InlineData("", false)]
    [InlineData("/workspaces/../etc", false)]
    [InlineData("/workspaces/a\nb", false)]
    [InlineData("/workspaces/a\tb", false)]
    public void WorkspaceFoldersAreChecked(string value, bool expected)
    {
        EnvironmentProfileValidator.Validate(Container() with { WorkspaceFolder = value }).Any(issue => string.Equals(issue.Property, "Container.WorkspaceFolder", StringComparison.Ordinal)).ShouldBe(!expected);
        EnvironmentProfileValidator.Validate(Distro() with { WorkspacePath = value }).Any(issue => string.Equals(issue.Property, "Wsl.WorkspacePath", StringComparison.Ordinal)).ShouldBe(!expected);
    }

    /// <summary>The podman path is a plain command name or a full path on this computer; a relative path or an option is refused.</summary>
    [Theory]
    [InlineData("podman", true)]
    [InlineData("/usr/bin/podman", true)]
    [InlineData("-x", false)]
    [InlineData("bin/podman", false)]
    [InlineData("./podman", false)]
    [InlineData("a b", false)]
    [InlineData("", false)]
    public void PodmanPathsAreChecked(string value, bool expected)
    {
        ContainerProfileRules.IsValidPodmanPath(value).ShouldBe(expected);
        EnvironmentProfileValidator.Validate(Container() with { PodmanPath = value }).Any(issue => string.Equals(issue.Property, "Container.PodmanPath", StringComparison.Ordinal)).ShouldBe(!expected);
    }

    /// <summary>The omp path inside the environment follows the rule of the ssh remote omp path.</summary>
    [Theory]
    [InlineData("omp", true)]
    [InlineData("/opt/omp/bin/omp", true)]
    [InlineData("-x", false)]
    [InlineData("rel/omp", false)]
    [InlineData("o m p", false)]
    public void OmpPathsAreChecked(string value, bool expected)
    {
        EnvironmentProfileValidator.Validate(Container() with { OmpPath = value }).Any(issue => string.Equals(issue.Property, "Container.OmpPath", StringComparison.Ordinal)).ShouldBe(!expected);
        EnvironmentProfileValidator.Validate(Distro() with { OmpPath = value }).Any(issue => string.Equals(issue.Property, "Wsl.OmpPath", StringComparison.Ordinal)).ShouldBe(!expected);
    }

    /// <summary>A WSL distribution name is a plain name; an option, a space or a shell character is refused.</summary>
    [Theory]
    [InlineData("Ubuntu-24.04", true)]
    [InlineData("Debian", true)]
    [InlineData("", false)]
    [InlineData("-d", false)]
    [InlineData("a b", false)]
    [InlineData("a;b", false)]
    [InlineData("a\nb", false)]
    [InlineData("a/b", false)]
    public void DistributionNamesAreChecked(string value, bool expected)
    {
        ContainerProfileRules.IsValidWslDistribution(value).ShouldBe(expected);
        EnvironmentProfileValidator.Validate(Distro() with { Distro = value }).Any(issue => string.Equals(issue.Property, "Wsl.Distro", StringComparison.Ordinal)).ShouldBe(!expected);
    }

    /// <summary>An environment carries the details of its own kind and no other: a profile never holds a second destination that could be picked by mistake.</summary>
    [Fact]
    public void DetailsBelongToTheirOwnKindOnly()
    {
        var container = new EnvironmentProfile { Id = "c1", DisplayName = "Dev box", Kind = EnvironmentKind.Container, Container = Container() };
        var wsl = new EnvironmentProfile { Id = "w1", DisplayName = "Ubuntu", Kind = EnvironmentKind.Wsl, Wsl = Distro() };
        EnvironmentProfileValidator.Validate(container).ShouldBeEmpty();
        EnvironmentProfileValidator.Validate(wsl).ShouldBeEmpty();

        EnvironmentProfileValidator.Validate(container with { Container = null }).ShouldContain(issue => issue.Property == "Container");
        EnvironmentProfileValidator.Validate(wsl with { Wsl = null }).ShouldContain(issue => issue.Property == "Wsl");
        EnvironmentProfileValidator.Validate(container with { Wsl = Distro() }).ShouldContain(issue => issue.Property == "Wsl");
        EnvironmentProfileValidator.Validate(wsl with { Container = Container() }).ShouldContain(issue => issue.Property == "Container");
        EnvironmentProfileValidator.Validate(container with { Ssh = new SshProfile { Alias = "a", RemoteWorkspaceRoot = "/w" } }).ShouldContain(issue => issue.Property == "Ssh");
        EnvironmentProfileValidator.Validate(container with { Kind = EnvironmentKind.Local }).ShouldContain(issue => issue.Property == "Container");
        EnvironmentProfileValidator.Validate(container with { Kind = EnvironmentKind.Ssh, Ssh = new SshProfile { Alias = "a", RemoteWorkspaceRoot = "/w" } }).ShouldContain(issue => issue.Property == "Container");
    }

    /// <summary>The details of a container environment are validated through the environment, with a prefixed property name.</summary>
    [Fact]
    public void ADetailIssueIsReportedThroughTheEnvironment()
    {
        var profile = new EnvironmentProfile { Id = "c1", DisplayName = "Dev box", Kind = EnvironmentKind.Container, Container = Container() with { Container = "-rm" } };

        EnvironmentProfileValidator.Validate(profile).ShouldContain(issue => issue.Property == "Container.Container");
    }

    /// <summary>An issue names the property and never repeats the rejected value.</summary>
    [Fact]
    public void IssuesNeverRepeatTheRejectedValue()
    {
        const string Hostile = "$(touch-pwned)";

        var issues = EnvironmentProfileValidator.Validate(Container() with { Container = Hostile, RemoteUser = Hostile, WorkspaceFolder = Hostile, PodmanPath = Hostile, OmpPath = Hostile });

        issues.Count.ShouldBe(5);
        issues.ShouldAllBe(issue => !issue.Message.Contains(Hostile, StringComparison.Ordinal));
    }
}
