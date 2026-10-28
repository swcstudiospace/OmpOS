using System.Text;
using Autonogrammer.Harness.Terminal;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpLauncher.Tests;

public sealed class OmpTerminalLauncherTests : IDisposable
{
    private const string VersionText = "omp 9.8.7";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly FakeOmp _fake = new();

    /// <inheritdoc />
    public void Dispose() => _fake.Dispose();

    [Fact]
    public async Task LaunchesLocatedOmpThroughTheTerminalBackendWithArgumentsAndProjectDirectory()
    {
        var projectDirectory = _fake.CreateDirectory("project dir with spaces");
        var binDirectory = _fake.CreateDirectory("bin");
        var path = _fake.WriteExecutable(binDirectory, $$"""
            if [ "$1" = "version" ]; then
              echo "{{VersionText}}"
              exit 0
            fi
            pwd -P
            printf 'ARG<%s>\n' "$@"
            """);
        var arguments = new[] { "--plugin-dir", "two words", "a;b", "$(echo injected)" };
        var sink = new CollectingSink();
        var launcher = new OmpTerminalLauncher(
            new LinuxLocalPtyBackend(),
            new OmpLocatorOptions(ConfiguredPath: null, [binDirectory], []));

        var result = await launcher.LaunchAsync(
            new OmpTerminalLaunchRequest(projectDirectory, arguments, 80, 24, sink),
            Token).ConfigureAwait(true);

        var started = result.ShouldBeOfType<OmpTerminalStarted>();
        await using (started.ConfigureAwait(true))
        {
            started.ExecutablePath.ShouldBe(path);
            started.Version.ShouldBe(VersionText);

            (await started.Session.WaitForExitAsync(Token).ConfigureAwait(true)).ShouldBe(0);

            var lines = sink.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lines.ShouldBe(
            [
                Path.GetFullPath(projectDirectory),
                "ARG<--plugin-dir>",
                "ARG<two words>",
                "ARG<a;b>",
                "ARG<$(echo injected)>",
            ]);
        }
    }

    [Fact]
    public async Task StartsTheLocatedExecutableWithTheExactOptionsEvenWhenTheVersionProbeFails()
    {
        var projectDirectory = _fake.CreateDirectory("project");
        var binDirectory = _fake.CreateDirectory("bin");
        var path = _fake.WriteExecutable(binDirectory, "exit 3");
        var sink = new CollectingSink();
        var backend = new RecordingBackend();
        var launcher = new OmpTerminalLauncher(backend, new OmpLocatorOptions(ConfiguredPath: null, [binDirectory], []));

        var result = await launcher.LaunchAsync(
            new OmpTerminalLaunchRequest(projectDirectory, ["acp"], 132, 43, sink),
            Token).ConfigureAwait(true);

        var started = result.ShouldBeOfType<OmpTerminalStarted>();
        await using (started.ConfigureAwait(true))
        {
            started.Version.ShouldBeNull();
            started.Session.ShouldBeSameAs(backend.Session);
            var options = backend.Options.ShouldHaveSingleItem();
            options.FileName.ShouldBe(path);
            options.Arguments.ShouldBe(["acp"]);
            options.WorkingDirectory.ShouldBe(projectDirectory);
            options.Columns.ShouldBe(132);
            options.Rows.ShouldBe(43);
            options.Output.ShouldBeSameAs(sink);
        }
    }

    [Fact]
    public async Task MissingOmpReturnsDetectedPathsVersionAndGuidanceWithoutThrowing()
    {
        var projectDirectory = _fake.CreateDirectory("project");
        var emptyBin = _fake.CreateDirectory("empty-bin");
        var configured = Path.Combine(_fake.CreateDirectory("configured"), "omp");
        var known = Path.Combine(_fake.CreateDirectory("known"), "omp");
        var backend = new RecordingBackend();
        var launcher = new OmpTerminalLauncher(backend, new OmpLocatorOptions(configured, [emptyBin], [known]));

        var result = await launcher.LaunchAsync(
            new OmpTerminalLaunchRequest(projectDirectory, [], 80, 24, new CollectingSink()),
            Token).ConfigureAwait(true);

        var notFound = result.ShouldBeOfType<OmpTerminalNotFound>();
        notFound.DetectedPaths.ShouldBe([configured, Path.Combine(emptyBin, OmpLocator.ExecutableName), known]);
        notFound.Version.ShouldBeNull();
        notFound.InstallGuidance.ShouldBe(OmpLocator.InstallGuidance);
        backend.Options.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingProjectDirectoryIsAFailureAndNoTerminalIsStarted()
    {
        var binDirectory = _fake.CreateDirectory("bin");
        _fake.WriteExecutable(binDirectory, "exit 0");
        var missing = Path.Combine(_fake.Root, "does-not-exist");
        var backend = new RecordingBackend();
        var launcher = new OmpTerminalLauncher(backend, new OmpLocatorOptions(ConfiguredPath: null, [binDirectory], []));

        var result = await launcher.LaunchAsync(
            new OmpTerminalLaunchRequest(missing, [], 80, 24, new CollectingSink()),
            Token).ConfigureAwait(true);

        var failed = result.ShouldBeOfType<OmpTerminalLaunchFailed>();
        failed.Reason.ShouldContain(missing, Case.Sensitive);
        backend.Options.ShouldBeEmpty();
    }

    [Fact]
    public async Task ABackendThatCannotStartTheSessionIsAFailureNotAnException()
    {
        var projectDirectory = _fake.CreateDirectory("project");
        var binDirectory = _fake.CreateDirectory("bin");
        _fake.WriteExecutable(binDirectory, "exit 0");
        var launcher = new OmpTerminalLauncher(
            new ThrowingBackend(new PlatformNotSupportedException("No terminal backend here.")),
            new OmpLocatorOptions(ConfiguredPath: null, [binDirectory], []));

        var result = await launcher.LaunchAsync(
            new OmpTerminalLaunchRequest(projectDirectory, [], 80, 24, new CollectingSink()),
            Token).ConfigureAwait(true);

        result.ShouldBeOfType<OmpTerminalLaunchFailed>().Reason.ShouldContain("No terminal backend here.", Case.Sensitive);
    }

    private sealed class CollectingSink : IProcessOutputSink
    {
        private readonly StringBuilder _text = new();
        private readonly Lock _gate = new();

        public string Text
        {
            get
            {
                lock (_gate)
                {
                    return _text.ToString();
                }
            }
        }

        public void Write(string text)
        {
            lock (_gate)
            {
                _text.Append(text);
            }
        }
    }

    private sealed class RecordingBackend : ITerminalBackend
    {
        public List<TerminalSessionOptions> Options { get; } = [];

        public StubSession Session { get; } = new();

        public Task<ITerminalSession> StartAsync(TerminalSessionOptions options, CancellationToken cancellationToken)
        {
            Options.Add(options);
            return Task.FromResult<ITerminalSession>(Session);
        }
    }

    private sealed class ThrowingBackend(Exception exception) : ITerminalBackend
    {
        public Task<ITerminalSession> StartAsync(TerminalSessionOptions options, CancellationToken cancellationToken) =>
            Task.FromException<ITerminalSession>(exception);
    }

    private sealed class StubSession : ITerminalSession
    {
        public Task WriteInputAsync(string text, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Resize(int columns, int rows)
        {
        }

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
