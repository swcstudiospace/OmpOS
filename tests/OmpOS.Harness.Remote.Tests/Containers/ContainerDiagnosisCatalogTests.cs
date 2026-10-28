using OmpOS.Contracts.Environments;
using OmpOS.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The words and failure rules of the container and WSL diagnoses.</summary>
public sealed class ContainerDiagnosisCatalogTests
{
    /// <summary>The text podman prints becomes the diagnosis the user can act on; the subject (container, user) is taken from the text.</summary>
    [Theory]
    [InlineData(125, "Error: no container with name or ID \"ghost\" found: no such container", RemoteDiagnosisKind.ContainerMissing)]
    [InlineData(125, "Error: no such container ghost", RemoteDiagnosisKind.ContainerMissing)]
    [InlineData(255, "Error: can only create exec sessions on running containers: container state improper", RemoteDiagnosisKind.ContainerNotRunning)]
    [InlineData(255, "Error: unable to find user nobody: no matching entries in passwd file", RemoteDiagnosisKind.ContainerUserMissing)]
    [InlineData(125, "Cannot connect to Podman. Please verify your connection", RemoteDiagnosisKind.ContainerRuntimeUnavailable)]
    [InlineData(127, "sh: 1: omp: not found", RemoteDiagnosisKind.OmpMissing)]
    [InlineData(71, "sh: cd: can't cd to /nope", RemoteDiagnosisKind.DirectoryMissing)]
    [InlineData(70, "mkdir: cannot create directory", RemoteDiagnosisKind.DirectoryNotWritable)]
    public void PodmanFailuresAreClassified(int exitCode, string standardError, RemoteDiagnosisKind expected)
    {
        var diagnosis = ContainerDiagnosisCatalog.FromPodmanFailure(exitCode, standardError).ShouldNotBeNull();

        diagnosis.Kind.ShouldBe(expected);
        diagnosis.Severity.ShouldBe(DiagnosisSeverity.Error);
        diagnosis.Detail.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A failure that is not recognised is left to the caller to report as unknown.</summary>
    [Theory]
    [InlineData(1, "something odd happened")]
    [InlineData(3, "")]
    [InlineData(125, "Error: unable to start container: crun: something")]
    public void UnknownFailuresAreNotGuessed(int exitCode, string standardError)
    {
        ContainerDiagnosisCatalog.FromPodmanFailure(exitCode, standardError).ShouldBeNull();
        ContainerDiagnosisCatalog.FromWslFailure(exitCode, standardError).ShouldBeNull();
    }

    /// <summary>The text wsl.exe prints (UTF-16 decoded as UTF-8, so NUL characters sit between the letters) becomes the diagnosis.</summary>
    [Theory]
    [InlineData("There is no distribution with the supplied name.", RemoteDiagnosisKind.WslDistroMissing)]
    [InlineData("T\0h\0e\0r\0e\0 \0i\0s\0 \0n\0o\0 \0d\0i\0s\0t\0r\0i\0b\0u\0t\0i\0o\0n\0 \0w\0i\0t\0h\0 \0t\0h\0e\0 \0s\0u\0p\0p\0l\0i\0e\0d\0 \0n\0a\0m\0e\0.\0", RemoteDiagnosisKind.WslDistroMissing)]
    [InlineData("There is no user with the supplied name.", RemoteDiagnosisKind.WslUserMissing)]
    [InlineData("Windows Subsystem for Linux has no installed distributions.", RemoteDiagnosisKind.WslDistroMissing)]
    [InlineData("The Windows Subsystem for Linux optional component is not enabled.", RemoteDiagnosisKind.WslMissing)]
    public void WslFailuresAreClassified(string standardError, RemoteDiagnosisKind expected)
    {
        var diagnosis = ContainerDiagnosisCatalog.FromWslFailure(1, standardError).ShouldNotBeNull();

        diagnosis.Kind.ShouldBe(expected);
        diagnosis.Detail.ShouldNotBeNull().ShouldNotContain("\0", Case.Sensitive);
    }

    /// <summary>The exit codes of the shared bootstrap mean the same in a distribution as in a container.</summary>
    [Fact]
    public void BootstrapExitCodesAreClassifiedForWsl()
    {
        ContainerDiagnosisCatalog.FromWslFailure(127, "x").ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.OmpMissing);
        ContainerDiagnosisCatalog.FromWslFailure(71, "x").ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.DirectoryMissing);
        ContainerDiagnosisCatalog.FromWslFailure(70, "x").ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.DirectoryNotWritable);
    }

    /// <summary>The wording names the container or the distribution, never "the remote machine", and holds the subject where it names one.</summary>
    [Fact]
    public void TheWordingNamesTheEnvironment()
    {
        var container = ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.OmpMissing, EnvironmentKind.Container);
        var wsl = ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.OmpMissing, EnvironmentKind.Wsl);
        var folder = ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.DirectoryMissing, EnvironmentKind.Container, subject: "/workspaces/x");

        container.Summary.ShouldContain("inside the container", Case.Sensitive);
        wsl.Summary.ShouldContain("in the WSL distribution", Case.Sensitive);
        container.Summary.ShouldNotContain("remote machine", Case.Insensitive);
        folder.Summary.ShouldContain("/workspaces/x", Case.Sensitive);
    }

    /// <summary>Every diagnosis kind has text for both environments, with a fix, and no container text names another runtime.</summary>
    [Fact]
    public void EveryKindHasTextForBothEnvironments()
    {
        var banned = string.Concat("dock", "er");
        foreach (var kind in Enum.GetValues<RemoteDiagnosisKind>())
        {
            foreach (var environment in new[] { EnvironmentKind.Container, EnvironmentKind.Wsl })
            {
                var diagnosis = ContainerDiagnosisCatalog.Create(kind, environment, subject: "SUBJECT");

                diagnosis.Kind.ShouldBe(kind);
                diagnosis.Summary.ShouldNotBeNullOrWhiteSpace();
                diagnosis.FixText.ShouldNotBeNullOrWhiteSpace();
                diagnosis.Summary.ShouldNotContain(banned, Case.Insensitive);
                diagnosis.FixText.ShouldNotContain(banned, Case.Insensitive);
            }
        }
    }

    /// <summary>Only container and WSL environments have these diagnoses.</summary>
    [Fact]
    public void OtherEnvironmentKindsAreRefused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.OmpMissing, EnvironmentKind.Ssh));
    }

    /// <summary>The text of a start failure is kept in the diagnosis but a diagnosis never holds more than a bounded excerpt.</summary>
    [Fact]
    public void TheDetailIsABoundedExcerpt()
    {
        var manyLines = string.Join('\n', Enumerable.Range(0, 20).Select(index => "line " + index));

        var diagnosis = ContainerDiagnosisCatalog.FromPodmanFailure(125, "Error: no such container x\n" + manyLines).ShouldNotBeNull();

        diagnosis.Detail.ShouldNotBeNull().Split('\n').Length.ShouldBeLessThanOrEqualTo(3);
    }
}
