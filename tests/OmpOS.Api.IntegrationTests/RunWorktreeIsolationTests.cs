using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using OmpOS.Api.Harness;
using OmpOS.Api.Options;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Sessions;
using OmpOS.Worktrees;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// A run starts omp in its own git worktree when both <c>Worktrees:Root</c> and <c>Worktrees:Repository</c> are set, and in the
/// process working directory when either is unset. Each run's worktree is removed when its session is disposed, which happens
/// on completion, failure and cancellation alike.
/// </summary>
public sealed class RunWorktreeIsolationTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "ompos-run-worktrees-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string RepositoryPath => Path.Combine(_workspace, "repo");

    private string RootPath => Path.Combine(_workspace, "worktrees");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    /// <summary>With either key unset, omp starts in the process working directory and no worktree root is created.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RunWithAnUnsetWorktreeKeyStartsOmpInTheProcessWorkingDirectory(bool setRoot, bool setRepository)
    {
        var observed = new ConcurrentQueue<WorkingDirectoryObservation>();
        var settings = new WorktreesOptions
        {
            Root = setRoot ? RootPath : string.Empty,
            Repository = setRepository ? RepositoryPath : string.Empty,
        };
        var factory = CreateFactory(settings, options =>
        {
            observed.Enqueue(new WorkingDirectoryObservation(options.WorkingDirectory, Directory.Exists(options.WorkingDirectory)));
            return new StubTransport(blockUntilCancelled: false);
        });

        var session = factory.CreateSession("prompt", "session-unset", RunId());
        await using (session)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => session.CreateAsync(Token)).ConfigureAwait(true);
        }

        observed.ShouldHaveSingleItem().WorkingDirectory.ShouldBe(Environment.CurrentDirectory);
        Directory.Exists(RootPath).ShouldBeFalse();
    }

    /// <summary>Two concurrent runs in one repository get separate worktrees; the main checkout is unchanged, and both worktrees are removed when their runs close.</summary>
    [Fact]
    public async Task TwoConcurrentRunsGetSeparateWorktreesAndTheMainCheckoutStaysUnchanged()
    {
        await CreateRepositoryAsync().ConfigureAwait(true);
        var headBefore = (await GitAsync(RepositoryPath, "rev-parse", "HEAD").ConfigureAwait(true)).Trim();
        var statusBefore = await GitAsync(RepositoryPath, "status", "--porcelain").ConfigureAwait(true);

        var observed = new ConcurrentQueue<WorkingDirectoryObservation>();
        var factory = CreateFactory(
            new WorktreesOptions { Root = RootPath, Repository = RepositoryPath },
            options =>
            {
                observed.Enqueue(new WorkingDirectoryObservation(options.WorkingDirectory, Directory.Exists(options.WorkingDirectory)));
                return new StubTransport(blockUntilCancelled: false);
            });

        var firstRunId = RunId();
        var secondRunId = RunId();
        var firstPath = Path.Combine(RootPath, firstRunId);
        var secondPath = Path.Combine(RootPath, secondRunId);
        var first = factory.CreateSession("first", "session-first", firstRunId);
        var second = factory.CreateSession("second", "session-second", secondRunId);
        try
        {
            await Task.WhenAll(
                Assert.ThrowsAnyAsync<Exception>(() => first.CreateAsync(Token)),
                Assert.ThrowsAnyAsync<Exception>(() => second.CreateAsync(Token))).ConfigureAwait(true);

            var starts = observed.ToArray();
            starts.Length.ShouldBe(2);
            starts.ShouldAllBe(start => start.ExistedWhenStarted);
            starts.Select(start => start.WorkingDirectory).ShouldBe([firstPath, secondPath], ignoreOrder: true);

            var listed = await GitAsync(RepositoryPath, "worktree", "list", "--porcelain").ConfigureAwait(true);
            listed.ShouldContain($"worktree {firstPath}\n");
            listed.ShouldContain($"worktree {secondPath}\n");

            // A file written by one run stays in that run's worktree, out of the other run and the main checkout.
            await File.WriteAllTextAsync(Path.Combine(firstPath, "agent-output.txt"), "written by the first run\n", Token).ConfigureAwait(true);
            File.Exists(Path.Combine(secondPath, "agent-output.txt")).ShouldBeFalse();
            File.Exists(Path.Combine(RepositoryPath, "agent-output.txt")).ShouldBeFalse();

            (await GitAsync(RepositoryPath, "rev-parse", "HEAD").ConfigureAwait(true)).Trim().ShouldBe(headBefore);
            (await GitAsync(RepositoryPath, "status", "--porcelain").ConfigureAwait(true)).ShouldBe(statusBefore);
        }
        finally
        {
            await first.DisposeAsync().ConfigureAwait(true);
            await second.DisposeAsync().ConfigureAwait(true);
        }

        Directory.Exists(firstPath).ShouldBeFalse();
        Directory.Exists(secondPath).ShouldBeFalse();
        var afterClose = await GitAsync(RepositoryPath, "worktree", "list", "--porcelain").ConfigureAwait(true);
        afterClose.ShouldNotContain($"worktree {firstPath}\n");
        afterClose.ShouldNotContain($"worktree {secondPath}\n");
        (await GitAsync(RepositoryPath, "for-each-ref", "--format=%(refname)", "refs/heads/").ConfigureAwait(true)).ShouldNotContain("refs/heads/ompos/run/");
        (await GitAsync(RepositoryPath, "status", "--porcelain").ConfigureAwait(true)).ShouldBe(statusBefore);
    }

    /// <summary>Cancelling a run while omp is starting removes that run's worktree when its session is disposed.</summary>
    [Fact]
    public async Task CancellingARunRemovesItsWorktree()
    {
        await CreateRepositoryAsync().ConfigureAwait(true);
        var observed = new ConcurrentQueue<WorkingDirectoryObservation>();
        var connecting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = CreateFactory(
            new WorktreesOptions { Root = RootPath, Repository = RepositoryPath },
            options =>
            {
                observed.Enqueue(new WorkingDirectoryObservation(options.WorkingDirectory, Directory.Exists(options.WorkingDirectory)));
                return new StubTransport(blockUntilCancelled: true, connecting: connecting);
            });

        using var cancellation = new CancellationTokenSource();
        var session = factory.CreateSession("prompt", "session-cancel", RunId());
        var create = Assert.ThrowsAnyAsync<Exception>(() => session.CreateAsync(cancellation.Token));
        await connecting.Task.WaitAsync(Token).ConfigureAwait(true);

        var start = observed.ShouldHaveSingleItem();
        start.ExistedWhenStarted.ShouldBeTrue();
        Directory.Exists(start.WorkingDirectory).ShouldBeTrue();

        await cancellation.CancelAsync().ConfigureAwait(true);
        await create.ConfigureAwait(true);
        await session.DisposeAsync().ConfigureAwait(true);

        Directory.Exists(start.WorkingDirectory).ShouldBeFalse();
        (await GitAsync(RepositoryPath, "worktree", "list", "--porcelain").ConfigureAwait(true)).ShouldNotContain(start.WorkingDirectory);
    }

    /// <summary>A relative <c>Worktrees:Root</c> or <c>Worktrees:Repository</c> fails startup, and the error names the key.</summary>
    [Theory]
    [InlineData("Worktrees:Root")]
    [InlineData("Worktrees:Repository")]
    public void RelativeWorktreePathFailsStartupNamingTheKey(string key)
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [key] = "relative/worktrees",
            [string.Equals(key, "Worktrees:Root", StringComparison.Ordinal) ? "Worktrees:Repository" : "Worktrees:Root"] = Path.Combine(_workspace, "absolute"),
        };
        using var factory = new OmpApiFactory(config);

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain(key);
    }

    /// <summary>Absolute worktree keys start the host normally.</summary>
    [Fact]
    public async Task AbsoluteWorktreeKeysStartTheHost()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Worktrees:Root"] = RootPath,
            ["Worktrees:Repository"] = RepositoryPath,
        };
        using var factory = new OmpApiFactory(config);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string RunId() => Guid.NewGuid().ToString("N");

    private static AcpHarnessSessionFactory CreateFactory(WorktreesOptions worktrees, Func<AcpClientOptions, IAcpTransport> transportFactory)
    {
        var repository = new InMemorySessionRepository();
        return new AcpHarnessSessionFactory(
            new OmpAcpClient(),
            Microsoft.Extensions.Options.Options.Create(new HarnessOptions { Adapter = HarnessOptions.AcpAdapter }),
            repository,
            new SessionService(repository, TimeProvider.System),
            transportFactory,
            AcpApprovalPolicy.DenyAll,
            TimeSpan.FromSeconds(30),
            Microsoft.Extensions.Options.Options.Create(worktrees));
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
            "user.name=OmpOS Test",
            "-c",
            "user.email=test@ompos.invalid",
            "-c",
            "commit.gpgsign=false",
            "commit",
            "--quiet",
            "-m",
            "initial").ConfigureAwait(true);
    }

    private static async Task<string> GitAsync(string workingDirectory, params string[] arguments)
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
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(Token).ConfigureAwait(true);
        var output = await stdout.ConfigureAwait(true);
        var error = await stderr.ConfigureAwait(true);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed with exit code {process.ExitCode}: {error.Trim()}");
        }

        return output;
    }

    /// <summary>The working directory omp was asked to start in, and whether that directory existed at the moment of the start.</summary>
    private sealed record WorkingDirectoryObservation(string WorkingDirectory, bool ExistedWhenStarted);

    /// <summary>
    /// Stands in for the omp process. It never answers the handshake: connecting either waits for cancellation or fails at once,
    /// so every run ends at the connection and the test observes the directory omp was started in.
    /// </summary>
    private sealed class StubTransport : IAcpTransport
    {
        private readonly bool _blockUntilCancelled;
        private readonly TaskCompletionSource? _connecting;

        public StubTransport(bool blockUntilCancelled, TaskCompletionSource? connecting = null)
        {
            _blockUntilCancelled = blockUntilCancelled;
            _connecting = connecting;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            _connecting?.TrySetResult();
            if (_blockUntilCancelled)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(true);
            }

            throw new InvalidOperationException("The stub transport has no engine to connect to.");
        }

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
