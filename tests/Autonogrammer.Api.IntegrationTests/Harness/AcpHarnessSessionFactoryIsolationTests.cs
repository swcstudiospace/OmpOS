using System.Collections.Concurrent;
using Autonogrammer.Api.Harness;
using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Sessions;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Harness;

/// <summary>
/// <see cref="AcpHarnessSessionFactory"/> hands the environment policy to every <see cref="AcpClientOptions"/> it builds and gives a run without a
/// worktree its own directory under <c>Harness:Acp:WorkspaceRoot</c>. The tests read the options the transport factory receives; no engine starts.
/// </summary>
public sealed class AcpHarnessSessionFactoryIsolationTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "autonogrammer-factory-isolation-" + Guid.NewGuid().ToString("N"));
    private readonly ConcurrentQueue<AcpClientOptions> _observed = new();

    private string Root => Path.Combine(_base, "workspaces");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }

    /// <summary>Production: the policy is Allowlist, and the engine starts in a directory of the session under the workspace root.</summary>
    [Fact]
    public async Task ProductionRunGetsAnAllowlistAndItsOwnDirectory()
    {
        var factory = CreateFactory(TestHostEnvironment.Production, new HarnessOptions.AcpSettings { WorkspaceRoot = Root });

        await using var session = factory.CreateSession("prompt", "session-prod", RunId());

        var options = _observed.ShouldHaveSingleItem();
        options.EnvironmentMode.ShouldBe(EngineEnvironmentMode.Allowlist);
        options.EnvironmentAllowList.ShouldBeEmpty();
        options.WorkingDirectory.ShouldBe(Path.Combine(Root, "session-prod"));
        Directory.Exists(options.WorkingDirectory).ShouldBeTrue();
        options.WorkingDirectory.ShouldNotBe(Environment.CurrentDirectory);
    }

    /// <summary>The configured allow-list reaches the options, and an explicit Inherit with the unsafe opt-in is passed as Inherit.</summary>
    [Fact]
    public async Task ConfiguredModeAndAllowListReachTheOptions()
    {
        var allowlist = CreateFactory(
            TestHostEnvironment.Production,
            new HarnessOptions.AcpSettings
            {
                WorkspaceRoot = Root,
                Environment = new HarnessOptions.AcpEnvironmentSettings { Allow = ["ANTHROPIC_API_KEY", "OPENAI_*"] },
            });
        var inherit = CreateFactory(
            TestHostEnvironment.Production,
            new HarnessOptions.AcpSettings
            {
                WorkspaceRoot = Root,
                AllowInheritedEnvironment = true,
                Environment = new HarnessOptions.AcpEnvironmentSettings { Mode = EngineEnvironmentMode.Inherit },
            });

        await using var first = allowlist.CreateSession("prompt", "session-allow", RunId());
        await using var second = inherit.CreateSession("prompt", "session-inherit", RunId());

        _observed.Count.ShouldBe(2);
        var options = _observed.ToArray();
        options[0].EnvironmentMode.ShouldBe(EngineEnvironmentMode.Allowlist);
        string[] allow = ["ANTHROPIC_API_KEY", "OPENAI_*"];
        options[0].EnvironmentAllowList.ToArray().ShouldBe(allow);
        options[1].EnvironmentMode.ShouldBe(EngineEnvironmentMode.Inherit);
    }

    /// <summary>Development keeps the desktop behaviour: the environment is inherited and the engine starts in the process working directory.</summary>
    [Fact]
    public async Task DevelopmentInheritsAndStartsInTheProcessWorkingDirectory()
    {
        var factory = CreateFactory(TestHostEnvironment.Development, new HarnessOptions.AcpSettings());

        await using var session = factory.CreateSession("prompt", "session-dev", RunId());

        var options = _observed.ShouldHaveSingleItem();
        options.EnvironmentMode.ShouldBe(EngineEnvironmentMode.Inherit);
        options.WorkingDirectory.ShouldBe(Environment.CurrentDirectory);
        Directory.Exists(Root).ShouldBeFalse();
    }

    /// <summary>A factory built without a host environment behaves like Development, so existing direct constructions keep their behaviour.</summary>
    [Fact]
    public async Task NoHostEnvironmentBehavesLikeDevelopment()
    {
        var factory = CreateFactory(environment: null, new HarnessOptions.AcpSettings());

        await using var session = factory.CreateSession("prompt", "session-none", RunId());

        _observed.ShouldHaveSingleItem().EnvironmentMode.ShouldBe(EngineEnvironmentMode.Inherit);
    }

    /// <summary>Development with a workspace root also uses it, so a developer can opt into the hosted layout.</summary>
    [Fact]
    public async Task DevelopmentWithAWorkspaceRootUsesIt()
    {
        var factory = CreateFactory(TestHostEnvironment.Development, new HarnessOptions.AcpSettings { WorkspaceRoot = Root });

        await using var session = factory.CreateSession("prompt", "session-dev-root", RunId());

        _observed.ShouldHaveSingleItem().WorkingDirectory.ShouldBe(Path.Combine(Root, "session-dev-root"));
    }

    /// <summary>Production with neither worktrees nor a workspace root refuses to start an engine and names the key.</summary>
    [Fact]
    public void ProductionWithoutAWorkingDirectoryOfItsOwnIsRefused()
    {
        var factory = CreateFactory(TestHostEnvironment.Production, new HarnessOptions.AcpSettings());

        var failure = Should.Throw<InvalidOperationException>(() => factory.CreateSession("prompt", "session-none", RunId()));

        failure.Message.ShouldContain("Harness:Acp:WorkspaceRoot");
        _observed.ShouldBeEmpty();
    }

    /// <summary>Production with worktrees configured needs no workspace root, creates no workspace directory, and defers the engine to the worktree.</summary>
    [Fact]
    public async Task ProductionWithWorktreesUsesNoWorkspaceDirectory()
    {
        var worktrees = new WorktreesOptions { Root = Path.Combine(_base, "worktrees"), Repository = Path.Combine(_base, "repository") };
        var factory = CreateFactory(TestHostEnvironment.Production, new HarnessOptions.AcpSettings { WorkspaceRoot = Root }, worktrees);

        await using var session = factory.CreateSession("prompt", "session-worktree", RunId());

        // The run's worktree is created when the session opens, and only then is the engine built in it.
        _observed.ShouldBeEmpty();
        Directory.Exists(Root).ShouldBeFalse();
    }

    /// <summary>A session identifier that could leave the workspace root is refused, and no engine starts or directory appears.</summary>
    [Theory]
    [InlineData("../escape")]
    [InlineData("..")]
    [InlineData("a/b")]
    public void TraversalInTheSessionIdentifierIsRefused(string sessionId)
    {
        var factory = CreateFactory(TestHostEnvironment.Production, new HarnessOptions.AcpSettings { WorkspaceRoot = Root });

        Should.Throw<ArgumentException>(() => factory.CreateSession("prompt", sessionId, RunId()));

        _observed.ShouldBeEmpty();
        Directory.Exists(Root).ShouldBeFalse();
        Directory.Exists(Path.Combine(_base, "escape")).ShouldBeFalse();
    }

    /// <summary>The prompt-only overload gets the policy too, and a generated directory under the workspace root.</summary>
    [Fact]
    public async Task PromptOnlyOverloadGetsThePolicyAndAGeneratedDirectory()
    {
        var factory = CreateFactory(TestHostEnvironment.Production, new HarnessOptions.AcpSettings { WorkspaceRoot = Root });

        await using var session = factory.CreateSession("prompt");

        var options = _observed.ShouldHaveSingleItem();
        options.EnvironmentMode.ShouldBe(EngineEnvironmentMode.Allowlist);
        Path.GetDirectoryName(options.WorkingDirectory).ShouldBe(Root);
        Path.GetFileName(options.WorkingDirectory).ShouldStartWith("prompt-");
        Directory.Exists(options.WorkingDirectory).ShouldBeTrue();
    }

    private static string RunId() => Guid.NewGuid().ToString("N");

    private AcpHarnessSessionFactory CreateFactory(IHostEnvironment? environment, HarnessOptions.AcpSettings acp, WorktreesOptions? worktrees = null)
    {
        var repository = new InMemorySessionRepository();
        Func<AcpClientOptions, IAcpTransport> transportFactory = options =>
        {
            _observed.Enqueue(options);
            return new IdleTransport();
        };
        return new AcpHarnessSessionFactory(
            new OmpAcpClient(transportFactory),
            Microsoft.Extensions.Options.Options.Create(new HarnessOptions { Adapter = HarnessOptions.AcpAdapter, Acp = acp }),
            repository,
            new SessionService(repository, TimeProvider.System),
            transportFactory,
            AcpApprovalPolicy.DenyAll,
            TimeSpan.FromSeconds(30),
            Microsoft.Extensions.Options.Options.Create(worktrees ?? new WorktreesOptions()),
            environment);
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
