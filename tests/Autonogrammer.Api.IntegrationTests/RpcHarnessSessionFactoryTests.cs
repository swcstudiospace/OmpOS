using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Autonogrammer.Api.Harness;
using Autonogrammer.Api.IntegrationTests.Harness;
using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Rpc;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// <see cref="OmpRpcHarnessSessionFactory"/> hands the isolation-resolved working directory, omp path and environment to every
/// <see cref="OmpRpcClientOptions"/> it builds, and shares the ACP factory's worktree session. The tests read the options the
/// seam receives; no engine starts.
/// </summary>
public sealed class RpcHarnessSessionFactoryTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "autonogrammer-rpc-factory-" + Guid.NewGuid().ToString("N"));
    private readonly ConcurrentQueue<OmpRpcClientOptions> _observed = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Root => Path.Combine(_base, "workspaces");

    private string RepositoryPath => Path.Combine(_base, "repo");

    private string WorktreeRootPath => Path.Combine(_base, "worktrees");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }

    /// <summary>The prompt-only overload resolves the workspace directory, omp path and allow-listed environment into the client options.</summary>
    [Fact]
    public async Task PromptOnlyOverloadResolvesDirectoryOmpPathAndAllowlistedEnvironment()
    {
        const string allowedName = "AUTONOGRAMMER_RPC_FACTORY_ALLOWED";
        const string secretName = "AUTONOGRAMMER_RPC_FACTORY_SECRET";
        Environment.SetEnvironmentVariable(allowedName, "kept");
        Environment.SetEnvironmentVariable(secretName, "dropped");
        try
        {
            var rpc = new HarnessOptions.RpcSettings { OmpPath = "/opt/omp", WorkspaceRoot = Root };
            rpc.Environment.Mode = EngineEnvironmentMode.Allowlist;
            rpc.Environment.Allow.Add(allowedName);
            var factory = CreateFactory(TestHostEnvironment.Production, rpc);

            await using var session = factory.CreateSession("prompt");

            var options = _observed.ShouldHaveSingleItem();
            options.OmpPath.ShouldBe("/opt/omp");
            Path.GetDirectoryName(options.Cwd).ShouldBe(Root);
            Path.GetFileName(options.Cwd).ShouldStartWith("prompt-");
            Directory.Exists(options.Cwd).ShouldBeTrue();
            var environment = options.EnvironmentVariables.ShouldNotBeNull();
            environment.ShouldContainKey("PATH");
            environment[allowedName].ShouldBe("kept");
            environment.ShouldNotContainKey(secretName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(allowedName, null);
            Environment.SetEnvironmentVariable(secretName, null);
        }
    }

    /// <summary>The per-run overload gives the session its own directory under the RPC workspace root, named by the session.</summary>
    [Fact]
    public async Task PerRunOverloadUsesTheSessionDirectoryUnderTheRpcWorkspaceRoot()
    {
        var factory = CreateFactory(TestHostEnvironment.Production, new HarnessOptions.RpcSettings { WorkspaceRoot = Root });

        await using var session = factory.CreateSession("prompt", "session-1", RunId());

        var options = _observed.ShouldHaveSingleItem();
        options.Cwd.ShouldBe(Path.Combine(Root, "session-1"));
        Directory.Exists(options.Cwd).ShouldBeTrue();
        options.EnvironmentVariables.ShouldNotBeNull("outside Development the default environment mode is Allowlist, never inherit");
    }

    /// <summary>Development keeps the desktop behaviour: the environment is inherited (null) and the engine starts in the process working directory.</summary>
    [Fact]
    public async Task DevelopmentInheritsEnvironmentAndStartsInTheProcessWorkingDirectory()
    {
        var factory = CreateFactory(TestHostEnvironment.Development, new HarnessOptions.RpcSettings());

        await using var session = factory.CreateSession("prompt", "session-1", RunId());

        var options = _observed.ShouldHaveSingleItem();
        options.Cwd.ShouldBe(Directory.GetCurrentDirectory());
        options.EnvironmentVariables.ShouldBeNull();
    }

    /// <summary>With worktrees configured, the session is built inside the run's worktree and disposing the session removes it.</summary>
    [Fact]
    public async Task PerRunWithWorktreesBuildsTheSessionInsideTheWorktreeAndRemovesItOnDispose()
    {
        await CreateRepositoryAsync().ConfigureAwait(true);
        var worktrees = new WorktreesOptions { Root = WorktreeRootPath, Repository = RepositoryPath };
        var factory = CreateFactory(TestHostEnvironment.Production, new HarnessOptions.RpcSettings(), worktrees);
        var runId = RunId();

        var session = factory.CreateSession("prompt", "session-1", runId);
        _observed.ShouldBeEmpty("the engine session is built only after its worktree exists");
        await session.CreateAsync(Token).ConfigureAwait(true);

        var options = _observed.ShouldHaveSingleItem();
        options.Cwd.ShouldStartWith(WorktreeRootPath);
        Directory.Exists(options.Cwd).ShouldBeTrue();
        var worktree = options.Cwd;

        await session.DisposeAsync().ConfigureAwait(true);

        Directory.Exists(worktree).ShouldBeFalse("disposing the run session removes its worktree");
    }

    /// <summary>Outside Development, with neither worktrees nor a workspace root, session creation is refused before any client is built.</summary>
    [Fact]
    public void ProductionWithoutAWorkingDirectoryOfItsOwnIsRefusedBeforeAnyClientIsBuilt()
    {
        var factory = CreateFactory(TestHostEnvironment.Production, new HarnessOptions.RpcSettings());

        var promptOnly = Assert.Throws<InvalidOperationException>(() => factory.CreateSession("prompt"));
        promptOnly.Message.ShouldContain("Harness:Rpc:WorkspaceRoot");
        var perRun = Assert.Throws<InvalidOperationException>(() => factory.CreateSession("prompt", "session-1", RunId()));
        perRun.Message.ShouldContain("Harness:Rpc:WorkspaceRoot");
        _observed.ShouldBeEmpty();
    }

    private static string RunId() => Guid.NewGuid().ToString("N");

    private OmpRpcHarnessSessionFactory CreateFactory(IHostEnvironment? environment, HarnessOptions.RpcSettings rpc, WorktreesOptions? worktrees = null)
    {
        return new OmpRpcHarnessSessionFactory(
            Microsoft.Extensions.Options.Options.Create(new HarnessOptions { Adapter = HarnessOptions.RpcAdapter, Rpc = rpc }),
            options =>
            {
                _observed.Enqueue(options);
                return new ScriptedSession();
            },
            Microsoft.Extensions.Options.Options.Create(worktrees ?? new WorktreesOptions()),
            environment);
    }

    private async Task CreateRepositoryAsync()
    {
        Directory.CreateDirectory(RepositoryPath);
        await GitAsync(RepositoryPath, "init", "--quiet").ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(RepositoryPath, "main.txt"), "main checkout\n", Token).ConfigureAwait(true);
        await GitAsync(RepositoryPath, "add", "main.txt").ConfigureAwait(true);
        await GitAsync(
            RepositoryPath,
            "-c",
            "user.name=Autonogrammer Test",
            "-c",
            "user.email=test@autonogrammer.invalid",
            "-c",
            "commit.gpgsign=false",
            "commit",
            "--quiet",
            "-m",
            "initial").ConfigureAwait(true);
    }

    private static async Task GitAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("git could not be started.");
        var stderr = process.StandardError.ReadToEndAsync(Token);
        await process.WaitForExitAsync(Token).ConfigureAwait(true);
        var error = await stderr.ConfigureAwait(true);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed with exit code {process.ExitCode}: {error.Trim()}");
        }
    }

    /// <summary>A session that never starts an engine: the tests only read the options it was created for.</summary>
    private sealed class ScriptedSession : IOmpSession
    {
        /// <inheritdoc />
        public HarnessState State => HarnessState.Ready;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> AdvertisedCapabilities => new Dictionary<string, string>(StringComparer.Ordinal);

        /// <inheritdoc />
        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <inheritdoc />
        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
            string prompt,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return new CompletedEvent("end_turn");
        }
    }
}
