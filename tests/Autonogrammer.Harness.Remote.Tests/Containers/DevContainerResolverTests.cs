using System.Text.Json;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>Finding the container of a dev container project: the definition (JSON with comments) and the containers the devcontainer CLI labelled with the project folder.</summary>
public sealed class DevContainerResolverTests
{
    // The legacy build-file and the compose-file property names of devcontainer.json, built from parts: Autonogrammer names no container runtime other than podman in its sources.
    private static readonly string LegacyBuildKey = string.Concat("dock", "erFile");
    private static readonly string ComposeKey = string.Concat("dock", "erComposeFile");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Project(TempDirectory directory, string definition, string relative = ".devcontainer/devcontainer.json")
    {
        var folder = directory.Combine("my proj");
        var path = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, definition);
        return folder;
    }

    private static string Container(string id, string name, string state, string? folder) =>
        "{\"Id\":\"" + id + "\",\"Names\":[\"" + name + "\"],\"Image\":\"registry.example/dev:1\",\"State\":\"" + state + "\",\"Labels\":"
        + (folder is null ? "null" : "{\"devcontainer.local_folder\":\"" + JsonEncodedText.Encode(folder) + "\"}") + "}";

    private static DevContainerResolver Resolver(FakeContainerHost host) => new(new PodmanContainerLister(host.ClientOptions));

    private const string FullDefinition = """
        {
          // The dev container of the project.
          "name": "My Dev",
          /* the image */
          "image": "registry.example/dev:1",
          "remoteUser": "dev",
          "workspaceFolder": "/workspaces/${localWorkspaceFolderBasename}",
        }
        """;

    /// <summary>The definition is read from JSON with comments and a trailing comma; the project folder name fills the workspace folder.</summary>
    [Fact]
    public async Task TheDefinitionIsReadFromJsonWithComments()
    {
        using var directory = TempDirectory.Create();
        var folder = Project(directory, FullDefinition);

        var definition = await DevContainerResolver.ReadDefinitionAsync(folder, Token).ConfigureAwait(true);

        definition.ShouldNotBeNull();
        definition.Name.ShouldBe("My Dev");
        definition.WorkspaceFolder.ShouldBe("/workspaces/my proj");
        definition.RemoteUser.ShouldBe("dev");
        definition.EffectiveUser.ShouldBe("dev");
        definition.BuildKind.ShouldBe(DevContainerBuildKind.Image);
        definition.Image.ShouldBe("registry.example/dev:1");
        definition.ConfigPath.ShouldBe(Path.Combine(folder, ".devcontainer", "devcontainer.json"));
    }

    /// <summary>Without a workspaceFolder the devcontainer default <c>/workspaces/&lt;folder name&gt;</c> applies, and the container user is the fallback user.</summary>
    [Fact]
    public async Task TheDefaultWorkspaceFolderAndUserFallbackApply()
    {
        using var directory = TempDirectory.Create();
        var folder = Project(directory, "{ \"containerUser\": \"worker\" }");

        var definition = await DevContainerResolver.ReadDefinitionAsync(folder, Token).ConfigureAwait(true);

        definition.ShouldNotBeNull();
        definition.WorkspaceFolder.ShouldBe("/workspaces/my proj");
        definition.RemoteUser.ShouldBeNull();
        definition.EffectiveUser.ShouldBe("worker");
        definition.BuildKind.ShouldBe(DevContainerBuildKind.None);
    }

    /// <summary>The local folder variable is filled in; a variable Autonogrammer cannot resolve leaves the workspace folder unknown, never a guessed path.</summary>
    [Fact]
    public async Task VariablesAreResolvedOrLeftUnknown()
    {
        using var directory = TempDirectory.Create();
        var withLocal = await DevContainerResolver.ReadDefinitionAsync(Project(directory, "{ \"workspaceFolder\": \"/mnt${localWorkspaceFolder}\" }"), Token).ConfigureAwait(true);
        using var other = TempDirectory.Create();
        var unresolved = await DevContainerResolver.ReadDefinitionAsync(Project(other, "{ \"workspaceFolder\": \"/w/${containerEnv:HOME}\" }"), Token).ConfigureAwait(true);
        using var relative = TempDirectory.Create();
        var notAbsolute = await DevContainerResolver.ReadDefinitionAsync(Project(relative, "{ \"workspaceFolder\": \"work\" }"), Token).ConfigureAwait(true);

        withLocal.ShouldNotBeNull().WorkspaceFolder.ShouldBe("/mnt" + Path.Combine(directory.Path, "my proj"));
        unresolved.ShouldNotBeNull().WorkspaceFolder.ShouldBeNull();
        notAbsolute.ShouldNotBeNull().WorkspaceFolder.ShouldBeNull();
    }

    /// <summary>The build kind tells how the container is made: an image, a build, a legacy build file, or a compose file.</summary>
    [Theory]
    [InlineData("{ \"image\": \"x\" }", DevContainerBuildKind.Image)]
    [InlineData("{ \"build\": { \"context\": \".\" } }", DevContainerBuildKind.Build)]
    [InlineData("{ \"image\": \"x\", \"build\": {} }", DevContainerBuildKind.Build)]
    [InlineData("{ }", DevContainerBuildKind.None)]
    public async Task TheBuildKindIsRecognised(string definition, DevContainerBuildKind expected)
    {
        using var directory = TempDirectory.Create();

        var result = await DevContainerResolver.ReadDefinitionAsync(Project(directory, definition), Token).ConfigureAwait(true);

        result.ShouldNotBeNull().BuildKind.ShouldBe(expected);
    }

    /// <summary>The legacy build-file property and the compose-file property are recognised by shape.</summary>
    [Fact]
    public async Task TheLegacyBuildAndComposePropertiesAreRecognised()
    {
        using var directory = TempDirectory.Create();
        var legacy = await DevContainerResolver.ReadDefinitionAsync(Project(directory, "{ \"" + LegacyBuildKey + "\": \"Containerfile\" }"), Token).ConfigureAwait(true);
        using var other = TempDirectory.Create();
        var compose = await DevContainerResolver.ReadDefinitionAsync(Project(other, "{ \"" + ComposeKey + "\": \"compose.yaml\", \"service\": \"app\" }"), Token).ConfigureAwait(true);

        legacy.ShouldNotBeNull().BuildKind.ShouldBe(DevContainerBuildKind.Build);
        compose.ShouldNotBeNull().BuildKind.ShouldBe(DevContainerBuildKind.Compose);
    }

    /// <summary>A definition at the project root (<c>.devcontainer.json</c>) is found too.</summary>
    [Fact]
    public async Task ARootDefinitionIsFound()
    {
        using var directory = TempDirectory.Create();
        var folder = Project(directory, "{ \"name\": \"Root\" }", ".devcontainer.json");

        var definition = await DevContainerResolver.ReadDefinitionAsync(folder, Token).ConfigureAwait(true);

        definition.ShouldNotBeNull().Name.ShouldBe("Root");
    }

    /// <summary>A folder without a definition gives null; a damaged, oversized or non-object definition is an error that says so.</summary>
    [Fact]
    public async Task MissingAndBrokenDefinitionsAreTold()
    {
        using var empty = TempDirectory.Create();
        (await DevContainerResolver.ReadDefinitionAsync(empty.Path, Token).ConfigureAwait(true)).ShouldBeNull();

        using var broken = TempDirectory.Create();
        await Should.ThrowAsync<InvalidDataException>(() => DevContainerResolver.ReadDefinitionAsync(Project(broken, "{ \"name\": "), Token)).ConfigureAwait(true);

        using var array = TempDirectory.Create();
        await Should.ThrowAsync<InvalidDataException>(() => DevContainerResolver.ReadDefinitionAsync(Project(array, "[1, 2]"), Token)).ConfigureAwait(true);

        using var large = TempDirectory.Create();
        var huge = "{ \"name\": \"" + new string('x', (1024 * 1024) + 16) + "\" }";
        await Should.ThrowAsync<InvalidDataException>(() => DevContainerResolver.ReadDefinitionAsync(Project(large, huge), Token)).ConfigureAwait(true);
    }

    /// <summary>The folder must be an absolute path.</summary>
    [Fact]
    public async Task ARelativeFolderIsRefused()
    {
        await Should.ThrowAsync<ArgumentException>(() => DevContainerResolver.ReadDefinitionAsync("relative/folder", Token)).ConfigureAwait(true);
    }

    /// <summary>One running container with the label of the folder gives the profile; podman is asked with the label filter and nothing is started.</summary>
    [Fact]
    public async Task OneRunningContainerGivesTheProfile()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();
        var folder = Project(directory, FullDefinition);
        host.SetFilteredPsOutput("[" + Container("a1", "devbox", "running", folder) + "]");

        var resolution = await Resolver(host).ResolveAsync(folder, Token).ConfigureAwait(true);

        resolution.Outcome.ShouldBe(DevContainerOutcome.Found);
        var profile = resolution.Profile.ShouldNotBeNull();
        profile.Container.ShouldBe("devbox");
        profile.RemoteUser.ShouldBe("dev");
        profile.WorkspaceFolder.ShouldBe("/workspaces/my proj");
        EnvironmentProfileValidator.Validate(profile).ShouldBeEmpty();
        var call = host.PodmanCalls().ShouldHaveSingleItem();
        call.ShouldContain("label=devcontainer.local_folder=" + folder, StringComparer.Ordinal);
        call[0].ShouldBe("ps");
    }

    /// <summary>A trailing separator on the folder does not hide the container.</summary>
    [Fact]
    public async Task ATrailingSeparatorIsTolerated()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();
        var folder = Project(directory, FullDefinition);
        host.SetFilteredPsOutput("[" + Container("a1", "devbox", "running", folder + "/") + "]");

        var resolution = await Resolver(host).ResolveAsync(folder, Token).ConfigureAwait(true);

        resolution.Outcome.ShouldBe(DevContainerOutcome.Found);
    }

    /// <summary>When no container exists the answer says to start it with the devcontainer CLI or podman, then attach: Autonogrammer never builds or starts a dev container.</summary>
    [Fact]
    public async Task NoContainerMeansStartItYourself()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();
        var folder = Project(directory, FullDefinition);
        host.SetFilteredPsOutput("[]");

        var resolution = await Resolver(host).ResolveAsync(folder, Token).ConfigureAwait(true);

        resolution.Outcome.ShouldBe(DevContainerOutcome.NotCreated);
        resolution.Profile.ShouldBeNull();
        resolution.FixText.ShouldNotBeNull().ShouldContain("start it with the devcontainer CLI or podman, then attach", Case.Sensitive);
        host.PodmanCalls().ShouldNotContain(call => call[0] == "start" || call[0] == "exec" || call[0] == "run");
    }

    /// <summary>A container labelled with another folder does not belong to this project.</summary>
    [Fact]
    public async Task AContainerOfAnotherFolderIsIgnored()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();
        var folder = Project(directory, FullDefinition);
        host.SetFilteredPsOutput("[" + Container("a1", "devbox", "running", directory.Combine("other project")) + "," + Container("b2", "plain", "running", null) + "]");

        var resolution = await Resolver(host).ResolveAsync(folder, Token).ConfigureAwait(true);

        resolution.Outcome.ShouldBe(DevContainerOutcome.NotCreated);
        resolution.Containers.ShouldBeEmpty();
    }

    /// <summary>A container that exists and is stopped is reported with the start command; the profile is not offered.</summary>
    [Fact]
    public async Task AStoppedContainerIsReported()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();
        var folder = Project(directory, FullDefinition);
        host.SetFilteredPsOutput("[" + Container("a1", "devbox", "exited", folder) + "]");

        var resolution = await Resolver(host).ResolveAsync(folder, Token).ConfigureAwait(true);

        resolution.Outcome.ShouldBe(DevContainerOutcome.NotRunning);
        resolution.Profile.ShouldBeNull();
        resolution.Containers.ShouldHaveSingleItem().Name.ShouldBe("devbox");
        resolution.FixText.ShouldNotBeNull().ShouldContain("podman start devbox", Case.Sensitive);
    }

    /// <summary>More than one running container with the label is ambiguous: the user picks.</summary>
    [Fact]
    public async Task TwoRunningContainersAreAmbiguous()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();
        var folder = Project(directory, FullDefinition);
        host.SetFilteredPsOutput("[" + Container("a1", "first", "running", folder) + "," + Container("b2", "second", "running", folder) + "]");

        var resolution = await Resolver(host).ResolveAsync(folder, Token).ConfigureAwait(true);

        resolution.Outcome.ShouldBe(DevContainerOutcome.Ambiguous);
        resolution.Profile.ShouldBeNull();
        resolution.Containers.Count.ShouldBe(2);
    }

    /// <summary>A folder without a definition is told so, before podman is run.</summary>
    [Fact]
    public async Task NoDefinitionIsToldBeforePodmanRuns()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();
        _ = Directory.CreateDirectory(directory.Combine("my proj"));

        var resolution = await Resolver(host).ResolveAsync(directory.Combine("my proj"), Token).ConfigureAwait(true);

        resolution.Outcome.ShouldBe(DevContainerOutcome.NoDefinition);
        host.PodmanCalls().ShouldBeEmpty();
    }

    /// <summary>A damaged definition and a workspace folder Autonogrammer cannot work out are told, with what to change, before podman is run.</summary>
    [Fact]
    public async Task InvalidDefinitionsAreToldBeforePodmanRuns()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();
        var broken = await Resolver(host).ResolveAsync(Project(directory, "{ nope"), Token).ConfigureAwait(true);
        using var other = TempDirectory.Create();
        var unresolved = await Resolver(host).ResolveAsync(Project(other, "{ \"workspaceFolder\": \"/w/${containerEnv:HOME}\" }"), Token).ConfigureAwait(true);

        broken.Outcome.ShouldBe(DevContainerOutcome.InvalidDefinition);
        broken.FixText.ShouldNotBeNull().ShouldContain("devcontainer.json", Case.Sensitive);
        unresolved.Outcome.ShouldBe(DevContainerOutcome.InvalidDefinition);
        unresolved.Definition.ShouldNotBeNull();
        unresolved.FixText.ShouldNotBeNull().ShouldContain("workspaceFolder", Case.Sensitive);
        host.PodmanCalls().ShouldBeEmpty();
    }

    /// <summary>A missing podman is a runtime problem with the install diagnosis, not an exception.</summary>
    [Fact]
    public async Task AMissingPodmanIsARuntimeProblem()
    {
        using var directory = TempDirectory.Create();
        var folder = Project(directory, FullDefinition);
        var resolver = new DevContainerResolver(new PodmanContainerLister(new PodmanClientOptions { PodmanPath = "/nonexistent/autonogrammer-test-podman" }));

        var resolution = await resolver.ResolveAsync(folder, Token).ConfigureAwait(true);

        resolution.Outcome.ShouldBe(DevContainerOutcome.RuntimeProblem);
        resolution.Diagnosis.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.ContainerRuntimeMissing);
        resolution.Definition.ShouldNotBeNull();
    }
}
