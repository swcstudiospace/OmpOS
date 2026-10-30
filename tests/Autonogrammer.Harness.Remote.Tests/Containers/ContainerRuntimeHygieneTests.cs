using Autonogrammer.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The container runtime of Autonogrammer is podman only: the container code, its tests, fixtures and docs name no other runtime and offer no option for one.</summary>
public sealed class ContainerRuntimeHygieneTests
{
    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName;
    }

    private static IEnumerable<string> ContainerFiles(string root)
    {
        string[] folders =
        [
            Path.Combine("src", "Autonogrammer.Harness.Remote", "Containers"),
            Path.Combine("tests", "Autonogrammer.Harness.Remote.Tests", "Containers"),
        ];
        foreach (var folder in folders)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }

        string[] single =
        [
            Path.Combine("tests", "Autonogrammer.Harness.Remote.Tests", "Fixtures", "fake-podman"),
            Path.Combine("tests", "Autonogrammer.Harness.Remote.Tests", "Fixtures", "fake-wsl"),
            Path.Combine("src", "Autonogrammer.Contracts", "Environments", "ContainerProfile.cs"),
            Path.Combine("src", "Autonogrammer.Contracts", "Environments", "ContainerProfileRules.cs"),
            Path.Combine("src", "Autonogrammer.Contracts", "Environments", "WslProfile.cs"),
            Path.Combine("src", "Autonogrammer.Presentation", "Remote", "ContainerCopy.cs"),
            Path.Combine("src", "Autonogrammer.Presentation", "Remote", "ContainerProfileEditor.cs"),
            Path.Combine("src", "Autonogrammer.Presentation", "Remote", "RunningContainerPicker.cs"),
            Path.Combine("src", "Autonogrammer.Presentation", "Remote", "RuntimeEnvironmentsViewModel.cs"),
            Path.Combine("docs", "runbooks", "remote-containers.md"),
            Path.Combine("docs", "decisions", "addendum-ADR-0015-containers.md"),
        ];
        foreach (var file in single)
        {
            yield return Path.Combine(root, file);
        }
    }

    /// <summary>No file of the container feature names another container runtime.</summary>
    [Fact]
    public void NoFileNamesAnotherRuntime()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Skip("The repository root (Autonogrammer.slnx) was not found above the test output.");
            return;
        }

        var banned = string.Concat("dock", "er");
        var files = ContainerFiles(root).ToList();
        files.Count.ShouldBeGreaterThan(20);
        var offenders = new List<string>();
        foreach (var file in files)
        {
            File.Exists(file).ShouldBeTrue($"{file} is expected to exist");
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains(banned, StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{Path.GetRelativePath(root, file)}:{index + 1}");
                }
            }
        }

        offenders.ShouldBeEmpty();
    }

    /// <summary>The default podman path is the plain command <c>podman</c> and no option type offers another runtime.</summary>
    [Fact]
    public void ThePodmanOptionsOfferNoOtherRuntime()
    {
        new PodmanClientOptions().PodmanPath.ShouldBe("podman");
        typeof(PodmanClientOptions).GetProperties().Select(property => property.Name).ShouldBe(["PodmanPath", "CommandTimeout"], ignoreOrder: true);
    }
}
