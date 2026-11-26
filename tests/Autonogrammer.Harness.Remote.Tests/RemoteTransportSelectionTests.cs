using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>
/// Remote parity (Phase 83, Task 6; ADR-0038 D2): each remote flavour — ssh, podman, WSL — composes an
/// <see cref="IAcpTransport"/> implementation from <c>Harness.Remote</c> itself and never an <c>Harness.Rpc</c> type.
/// The composed session factory is <c>Func&lt;AcpClientOptions, IAcpTransport&gt;</c> for every flavour, so an RPC
/// client cannot be substituted without changing the signature (and the Task 2 edge assertion would fail first).
/// No live remote host: the fake hosts only build a profile and launch.
/// </summary>
public sealed class RemoteTransportSelectionTests
{
    /// <summary>The ssh flavour composes an <see cref="SshAcpTransport"/> over ACP.</summary>
    [Fact]
    public void SshFlavourComposesAnSshAcpTransport()
    {
        using var host = FakeRemoteHost.Create();
        var launch = LaunchFactory.Create(host.WorkspaceDirectory, Path.Combine(host.HomeDirectory, ".autonogrammer", "sessions", "s1"));
        var factory = SshAcpTransportFactory.Create(host.Profile, launch, new SshCommandBuilder(host.ClientOptions));

        var transport = factory(new AcpClientOptions());

        transport.ShouldBeOfType<SshAcpTransport>();
        transport.ShouldBeAssignableTo<IAcpTransport>();
    }

    /// <summary>The podman flavour composes a <see cref="PodmanAcpTransport"/> over ACP.</summary>
    [Fact]
    public void PodmanFlavourComposesAPodmanAcpTransport()
    {
        using var host = FakeContainerHost.Create();
        var launch = LaunchFactory.Create(host.WorkspaceDirectory, Path.Combine(host.HomeDirectory, ".autonogrammer", "sessions", "s1"));
        var factory = PodmanAcpTransportFactory.Create(host.Profile, launch, new PodmanExecCommandBuilder(host.ClientOptions));

        var transport = factory(new AcpClientOptions());

        transport.ShouldBeOfType<PodmanAcpTransport>();
        transport.ShouldBeAssignableTo<IAcpTransport>();
    }

    /// <summary>The WSL flavour composes a <see cref="WslAcpTransport"/> over ACP.</summary>
    [Fact]
    public void WslFlavourComposesAWslAcpTransport()
    {
        using var host = FakeWslHost.Create();
        var launch = LaunchFactory.Create(host.WorkspaceDirectory, Path.Combine(host.HomeDirectory, ".autonogrammer", "sessions", "s1"));
        var factory = WslAcpTransportFactory.Create(host.Profile, launch, new WslCommandBuilder(host.ClientOptions));

        var transport = factory(new AcpClientOptions());

        transport.ShouldBeOfType<WslAcpTransport>();
        transport.ShouldBeAssignableTo<IAcpTransport>();
    }

    /// <summary><c>Harness.Remote</c> references <c>Harness.Acp</c> and never <c>Harness.Rpc</c> (ADR-0038 D2).</summary>
    [Fact]
    public void RemoteAssemblyReferencesAcpAndNeverRpc()
    {
        var referenced = typeof(SshAcpTransportFactory).Assembly.GetReferencedAssemblies().Select(static a => a.Name).ToArray();

        referenced.ShouldContain("Autonogrammer.Harness.Acp", StringComparer.Ordinal);
        referenced.ShouldNotContain("Autonogrammer.Harness.Rpc", StringComparer.Ordinal);
    }
}
