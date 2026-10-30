using System.Reflection;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Composition;
using Autonogrammer.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>
/// The services a head hands to the shell and to the Settings window: after the services are opened over fakes (temporary roots, no network) and the head is built over them, every member
/// the code reads is present, so a missing wire shows as a failing test rather than as a page that quietly does nothing.
/// </summary>
public sealed class HeadServicesWiringTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static HeadServicesOptions Options(TempTree tree, Func<string, string?>? variables = null) => new()
    {
        StateRoot = tree.State,
        PluginsRoot = tree.Plugins,
        WorkspaceRoot = tree.Workspace,
        HomeDirectory = Path.Combine(tree.Root, "home"),
        ConfigDirectory = Path.Combine(tree.Root, "config"),
        GetVariable = variables ?? (static _ => null),
        ApiHandler = new ThrowingHandler(),
    };

    /// <summary>Opened over fakes with no cloud address, every service exists except the cloud host, which is absent on purpose; the best-of-N host appears once the head is built.</summary>
    [Fact]
    public async Task EveryServiceIsPresentOnceTheHeadIsBuiltWithoutACloudAddress()
    {
        using var tree = new TempTree();
        var services = await HeadServices.CreateAsync(Options(tree), Token).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);
        services.BestOfN.ShouldBeNull("the engine set creates it when the head is built");

        var head = AgentHead.Create(AgentLoopFixtures.Options(tree, null, services: services));
        await using var headScope = head.ConfigureAwait(true);

        head.Services.ShouldBeSameAs(services);
        services.BestOfN.ShouldNotBeNull();
        services.Cloud.ShouldBeNull("no AUTONOGRAMMER_API_BASE_URL is set");
        MissingMembers(services, allowedNull: [nameof(HeadServices.Cloud), nameof(HeadServices.TrustTerminalOpener), nameof(HeadServices.PluginsRoot), nameof(HeadServices.LoggerFactory)]).ShouldBeEmpty();
        services.McpContext([tree.Workspace]).WorkspaceRoots.ShouldBe([tree.Workspace]);
        services.CreateModeSkills(tree.Workspace).ShouldNotBeNull();
    }

    /// <summary>With an API address the cloud host exists as well; creating it makes no request.</summary>
    [Fact]
    public async Task TheCloudHostExistsWhenAnApiAddressIsConfigured()
    {
        using var tree = new TempTree();
        var variables = new Func<string, string?>(name => name switch
        {
            HeadServices.ApiBaseUrlVariable => "https://api.example.test/",
            HeadServices.ApiTokenVariable => "fake-token-for-tests",
            _ => null,
        });
        var services = await HeadServices.CreateAsync(Options(tree, variables), Token).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);

        services.Cloud.ShouldNotBeNull();
        MissingMembers(services, allowedNull: [nameof(HeadServices.BestOfN), nameof(HeadServices.TrustTerminalOpener), nameof(HeadServices.PluginsRoot), nameof(HeadServices.LoggerFactory)]).ShouldBeEmpty();
    }

    /// <summary>The automations are created with the services, started only when the head opens, and report whether a local automation can run from whether omp is installed.</summary>
    [Fact]
    public async Task TheAutomationsStartWhenTheHeadOpens()
    {
        using var tree = new TempTree();
        var services = await HeadServices.CreateAsync(Options(tree) with { OmpAvailable = static () => true }, Token).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);
        var head = AgentHead.Create(AgentLoopFixtures.Options(tree, null, services: services));
        await using var headScope = head.ConfigureAwait(true);

        services.Automations.IsStarted.ShouldBeFalse("the schedules count only once the head is open");
        services.Automations.LocalAvailable.ShouldBeTrue();
        services.Automations.CloudAvailable.ShouldBeFalse("no AUTONOGRAMMER_API_BASE_URL is set");
        services.Automations.Manager.SupportedTargetKinds.ShouldBe(["local"]);

        await head.InitializeAsync(Token).ConfigureAwait(true);

        services.Automations.IsStarted.ShouldBeTrue();
    }

    /// <summary>A head without omp installed has no local automations, and the container hosts carry the podman options the head was given.</summary>
    [Fact]
    public async Task WithoutOmpThereAreNoLocalAutomationsAndThePodmanOptionsAreShared()
    {
        using var tree = new TempTree();
        var podman = new Autonogrammer.Harness.Remote.Containers.PodmanClientOptions { PodmanPath = "/opt/podman/bin/podman" };
        var services = await HeadServices.CreateAsync(Options(tree) with { OmpAvailable = static () => false, Podman = podman }, Token).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);

        services.Automations.LocalAvailable.ShouldBeFalse();
        services.Automations.Manager.SupportedTargetKinds.ShouldBeEmpty();
        services.PodmanOptions.ShouldBeSameAs(podman);
        services.ContainerEnvironments.SupportsWsl.ShouldBe(OperatingSystem.IsWindows());
    }

    /// <summary>Returns the names of the public properties that are null although they are not declared nullable, and of the declared-nullable ones that are not in <paramref name="allowedNull"/> and are null.</summary>
    private static List<string> MissingMembers(HeadServices services, string[] allowedNull)
    {
        var context = new NullabilityInfoContext();
        var missing = new List<string>();
        foreach (var property in typeof(HeadServices).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.GetMethod is null)
            {
                continue;
            }

            var value = property.GetValue(services);
            var declaredNullable = context.Create(property).ReadState == NullabilityState.Nullable;
            if (value is null && (!declaredNullable || !allowedNull.Contains(property.Name, StringComparer.Ordinal)))
            {
                missing.Add(property.Name);
            }
        }

        return missing;
    }

    /// <summary>An API handler that fails when used: opening the services must not call the API.</summary>
    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Opening the head services made an API request.");
    }
}
