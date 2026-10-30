using System.Collections.Concurrent;
using Autonogrammer.Api.Harness;
using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Harness;

/// <summary>
/// <see cref="AcpHarnessSessionFactory"/> builds the engine's approval policy from <c>Harness:Acp:Approval:Kinds</c>: nothing configured denies every kind,
/// and a configured kind is admitted to the approval flow. The tests read the <see cref="AcpClientOptions"/> the transport factory receives; no engine starts.
/// </summary>
public sealed class AcpHarnessSessionFactoryApprovalTests
{
    private readonly ConcurrentQueue<AcpClientOptions> _observed = new();

    /// <summary>With the default options the factory hands the engine a policy that denies every kind.</summary>
    [Fact]
    public async Task DefaultOptionsDenyEveryKind()
    {
        var factory = CreateFactory(new HarnessApprovalOptions());

        await using var session = factory.CreateSession("prompt");

        var policy = _observed.ShouldHaveSingleItem().ApprovalPolicy;
        foreach (var kind in HarnessApprovalOptions.AcpToolKinds)
        {
            policy.Allows(new AcpPermissionRequest("call", "tool", kind, "{}")).ShouldBeFalse(kind);
        }
    }

    /// <summary>A configured kind is admitted and every other kind stays denied.</summary>
    [Fact]
    public async Task ConfiguredKindsAreAdmittedAndOthersDenied()
    {
        var factory = CreateFactory(new HarnessApprovalOptions { Kinds = ["execute", "edit"] });

        await using var session = factory.CreateSession("prompt");

        var policy = _observed.ShouldHaveSingleItem().ApprovalPolicy;
        policy.Allows(new AcpPermissionRequest("call", "tool", "execute", "{}")).ShouldBeTrue();
        policy.Allows(new AcpPermissionRequest("call", "tool", "edit", "{}")).ShouldBeTrue();
        policy.Allows(new AcpPermissionRequest("call", "tool", "read", "{}")).ShouldBeFalse();
        policy.Allows(new AcpPermissionRequest("call", "tool", string.Empty, "{}")).ShouldBeFalse();
    }

    /// <summary>The decision timeout is the shared 30 seconds when unset, and the configured number of seconds otherwise.</summary>
    [Fact]
    public void DecisionTimeoutDefaultsToTheSharedWindow()
    {
        new HarnessApprovalOptions().DecisionTimeout.ShouldBe(AcpHarnessSessionFactory.PermissionDecisionTimeout);
        new HarnessApprovalOptions { DecisionTimeoutSeconds = 120 }.DecisionTimeout.ShouldBe(TimeSpan.FromSeconds(120));
    }

    private AcpHarnessSessionFactory CreateFactory(HarnessApprovalOptions approval)
    {
        var repository = new InMemorySessionRepository();
        Func<AcpClientOptions, IAcpTransport> transportFactory = options =>
        {
            _observed.Enqueue(options);
            return new IdleTransport();
        };
        return new AcpHarnessSessionFactory(
            new OmpAcpClient(transportFactory),
            Microsoft.Extensions.Options.Options.Create(new HarnessOptions { Adapter = HarnessOptions.AcpAdapter, Acp = new HarnessOptions.AcpSettings { Approval = approval } }),
            repository,
            new SessionService(repository, TimeProvider.System));
    }

    /// <summary>A transport that is never connected: the tests only read the options it was created for.</summary>
    private sealed class IdleTransport : IAcpTransport
    {
        public Task ConnectAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("The idle transport has no engine to connect to.");

        public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(true);
            yield break;
        }

        public Task CloseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
