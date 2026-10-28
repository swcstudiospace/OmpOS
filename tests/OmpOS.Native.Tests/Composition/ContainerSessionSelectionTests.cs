using OmpOS.Contracts.Environments;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Remote.Containers;
using OmpOS.Harness.Remote.Tests;
using OmpOS.Native.Agents;
using OmpOS.Native.Composition;
using OmpOS.Native.Tests.AgentLoop;
using OmpOS.Presentation.Remote;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Composition;

/// <summary>
/// Which environment a new session runs in, for the container and WSL profiles: the choice of the New session picker reaches the engine set, and a container profile starts omp through podman exec (the
/// shared stand-in script, no real podman, no network) and never through the head's local factory.
/// </summary>
public sealed class ContainerSessionSelectionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<HeadServices> ServicesAsync(TempTree tree, FakeContainerHost? container) =>
        await HeadServices.CreateAsync(
            new HeadServicesOptions
            {
                StateRoot = tree.State,
                PluginsRoot = tree.Plugins,
                WorkspaceRoot = tree.Workspace,
                HomeDirectory = Path.Combine(tree.Root, "home"),
                ConfigDirectory = Path.Combine(tree.Root, "config"),
                GetVariable = static _ => null,
                Podman = container?.ClientOptions,
                OmpAvailable = static () => false,
            },
            Token).ConfigureAwait(true);

    private static EnvironmentProfile ContainerProfile(FakeContainerHost container) => new()
    {
        Id = "devbox",
        DisplayName = "Dev box",
        Kind = EnvironmentKind.Container,
        Container = container.Profile,
    };

    /// <summary>A session started on a container profile connects through podman exec: the fake omp in the container answers, the local factory is never asked, and the environment is remembered as used.</summary>
    [Fact]
    public async Task AContainerSessionRunsOmpThroughPodmanExec()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The podman stand-in is a POSIX shell script.");
        using var tree = new TempTree();
        using var container = FakeContainerHost.Create();
        var services = await ServicesAsync(tree, container).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);
        var profile = ContainerProfile(container);
        await services.RemoteProfiles.SaveAsync(profile, Token).ConfigureAwait(true);
        var local = new ScriptedOmp();
        var head = AgentHead.Create(AgentLoopFixtures.Options(tree, local, services: services));
        await using var headScope = head.ConfigureAwait(true);
        services.NewSessionTarget.Set(new SessionTarget(SessionTargetKind.Container, "container: Dev box", profile));

        var (id, slot) = await AgentLoopFixtures.NewSlotAsync(head, Token).ConfigureAwait(true);
        var events = await AgentLoopFixtures.SendAsync(slot, "hello-container", Token).ConfigureAwait(true);

        var plan = await slot.PlanAsync(Token).ConfigureAwait(true);
        plan.Extras.EnvironmentId.ShouldBe("devbox");
        plan.Extras.IsRemote.ShouldBeTrue();
        (await slot.RemoteResourcesAsync(Token).ConfigureAwait(true)).ShouldBeOfType<ContainerSessionResources>();
        events.OfType<TextDeltaEvent>().Select(static delta => delta.Text).ShouldContain(static text => text.StartsWith("echo:hello-container", StringComparison.Ordinal));
        local.Launches.ShouldBeEmpty("the head's own factory is for local sessions");
        container.PodmanCalls().Any(static call => call.Contains("exec", StringComparer.Ordinal) && call.Contains("devbox", StringComparer.Ordinal)).ShouldBeTrue("omp was started with podman exec in the container");
        container.OmpLog().ShouldContain(static line => line.StartsWith("ARGV:", StringComparison.Ordinal) && line.Contains("acp", StringComparison.Ordinal));
        var listing = await services.RemoteProfiles.ListAsync(Token).ConfigureAwait(true);
        listing.Profiles.Single().LastUsedUtc.ShouldNotBeNull("starting a session records the environment as used");
        id.ShouldNotBeNullOrEmpty();
    }

    /// <summary>A profile whose environment is gone from the saved ones fails with a sentence that names the Remote page, not with a null.</summary>
    [Fact]
    public async Task ASessionOnAnUnknownEnvironmentSaysHowToFixIt()
    {
        using var tree = new TempTree();
        var services = await ServicesAsync(tree, container: null).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);
        var head = AgentHead.Create(AgentLoopFixtures.Options(tree, new ScriptedOmp(), services: services));
        await using var headScope = head.ConfigureAwait(true);
        var ghost = new EnvironmentProfile
        {
            Id = "ghost",
            DisplayName = "Ghost",
            Kind = EnvironmentKind.Container,
            Container = new ContainerProfile { Container = "ghost", WorkspaceFolder = "/work" },
        };
        services.NewSessionTarget.Set(new SessionTarget(SessionTargetKind.Container, "container: Ghost", ghost));

        var (_, slot) = await AgentLoopFixtures.NewSlotAsync(head, Token).ConfigureAwait(true);
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => slot.EngineAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("Remote page", Case.Sensitive);
    }

    /// <summary>Where WSL is not supported a WSL profile is kept but a session on it is refused with the sentence that says WSL runs on Windows only; wsl.exe is never started.</summary>
    [Fact]
    public async Task AWslSessionIsRefusedWhereWslIsNotSupported()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "WSL is supported on Windows.");
        using var tree = new TempTree();
        var services = await ServicesAsync(tree, container: null).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);
        var wsl = new EnvironmentProfile
        {
            Id = "ubuntu",
            DisplayName = "Ubuntu",
            Kind = EnvironmentKind.Wsl,
            Wsl = new WslProfile { Distro = "Ubuntu", WorkspacePath = "/home/dev/app" },
        };
        await services.RemoteProfiles.SaveAsync(wsl, Token).ConfigureAwait(true);
        var head = AgentHead.Create(AgentLoopFixtures.Options(tree, new ScriptedOmp(), services: services));
        await using var headScope = head.ConfigureAwait(true);
        services.NewSessionTarget.Set(new SessionTarget(SessionTargetKind.Wsl, "wsl: Ubuntu", wsl));

        var (_, slot) = await AgentLoopFixtures.NewSlotAsync(head, Token).ConfigureAwait(true);
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => slot.EngineAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldBe(ContainerCopy.WslWindowsOnly);
    }
}
