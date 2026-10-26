using System.Text;
using OmpOS.Harness.Terminal;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.OmpLauncher.Tests;

/// <summary>
/// Verifies that the launcher hands the request's environment to the terminal backend unchanged, that a command started
/// through a real pseudo-terminal sees those variables, that an invalid variable is a typed failure without a value in
/// it, and that no value is ever printed. The omp executables are fake shell scripts.
/// </summary>
public sealed class OmpTerminalLauncherEnvironmentTests : IDisposable
{
    private const string SecretValue = "s3cret-value-9012";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly FakeOmp _fake = new();

    /// <inheritdoc />
    public void Dispose() => _fake.Dispose();

    [Fact]
    public void TheRequestEnvironmentIsEmptyByDefault()
    {
        var request = new OmpTerminalLaunchRequest("/tmp", [], 80, 24, new CollectingSink());

        request.Environment.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheRequestEnvironmentReachesTheBackendOptionsAndTheOtherFieldsAreUnchanged()
    {
        var projectDirectory = _fake.CreateDirectory("project");
        var binDirectory = _fake.CreateDirectory("bin");
        _fake.WriteExecutable(binDirectory, "exit 0");
        var backend = new RecordingBackend();
        var launcher = new OmpTerminalLauncher(backend, new OmpLocatorOptions(ConfiguredPath: null, [binDirectory], []));
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PI_CODING_AGENT_DIR"] = "/tmp/agent",
            ["ULTRATHINK_UPLIFT"] = "0",
        };

        var result = await launcher.LaunchAsync(
            new OmpTerminalLaunchRequest(projectDirectory, ["--no-extensions"], 120, 40, new CollectingSink()) { Environment = environment },
            Token).ConfigureAwait(true);

        var started = result.ShouldBeOfType<OmpTerminalStarted>();
        await using (started.ConfigureAwait(true))
        {
            var options = backend.Options.ShouldHaveSingleItem();
            options.Environment.ShouldBeSameAs(environment);
            options.Arguments.ShouldBe(["--no-extensions"]);
            options.WorkingDirectory.ShouldBe(projectDirectory);
            options.Columns.ShouldBe(120);
            options.Rows.ShouldBe(40);
        }
    }

    [Fact]
    public async Task AFakeOmpStartedThroughARealTerminalSeesTheRequestEnvironment()
    {
        var projectDirectory = _fake.CreateDirectory("project");
        var binDirectory = _fake.CreateDirectory("bin");
        _fake.WriteExecutable(binDirectory, """
            if [ "$1" = "version" ]; then
              echo "omp 9.8.7"
              exit 0
            fi
            printf 'VALUE<%s>\n' "${OMPOS_LAUNCH_TEST:-absent}"
            """);
        var sink = new CollectingSink();
        var launcher = new OmpTerminalLauncher(
            new LinuxLocalPtyBackend(),
            new OmpLocatorOptions(ConfiguredPath: null, [binDirectory], []));

        var result = await launcher.LaunchAsync(
            new OmpTerminalLaunchRequest(projectDirectory, [], 80, 24, sink)
            {
                Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["OMPOS_LAUNCH_TEST"] = "from the plan" },
            },
            Token).ConfigureAwait(true);

        var started = result.ShouldBeOfType<OmpTerminalStarted>();
        await using (started.ConfigureAwait(true))
        {
            (await started.Session.WaitForExitAsync(Token).ConfigureAwait(true)).ShouldBe(0);

            sink.Text.ShouldContain("VALUE<from the plan>", Case.Sensitive);
        }
    }

    [Fact]
    public async Task AnInvalidVariableNameIsATypedFailureWithoutTheValueAndNoTerminalIsStarted()
    {
        var projectDirectory = _fake.CreateDirectory("project");
        var binDirectory = _fake.CreateDirectory("bin");
        _fake.WriteExecutable(binDirectory, "exit 0");
        var backend = new RecordingBackend();
        var launcher = new OmpTerminalLauncher(backend, new OmpLocatorOptions(ConfiguredPath: null, [binDirectory], []));

        var result = await launcher.LaunchAsync(
            new OmpTerminalLaunchRequest(projectDirectory, [], 80, 24, new CollectingSink())
            {
                Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["BAD=NAME"] = SecretValue },
            },
            Token).ConfigureAwait(true);

        var failed = result.ShouldBeOfType<OmpTerminalLaunchFailed>();
        failed.Reason.ShouldContain("BAD=NAME", Case.Sensitive);
        failed.Reason.ShouldNotContain(SecretValue, Case.Sensitive);
        backend.Options.ShouldBeEmpty();
    }

    [Fact]
    public async Task AMissingOmpStaysATypedResultWhenTheRequestCarriesAnEnvironment()
    {
        var projectDirectory = _fake.CreateDirectory("project");
        var emptyBin = _fake.CreateDirectory("empty-bin");
        var launcher = new OmpTerminalLauncher(new RecordingBackend(), new OmpLocatorOptions(ConfiguredPath: null, [emptyBin], []));

        var result = await launcher.LaunchAsync(
            new OmpTerminalLaunchRequest(projectDirectory, [], 80, 24, new CollectingSink())
            {
                Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["PI_CODING_AGENT_DIR"] = "/tmp/agent" },
            },
            Token).ConfigureAwait(true);

        result.ShouldBeOfType<OmpTerminalNotFound>();
    }

    [Fact]
    public void ToStringListsTheVariableNamesAndNeverTheValuesOrTheArguments()
    {
        var request = new OmpTerminalLaunchRequest("/tmp/project", ["--token", SecretValue], 80, 24, new CollectingSink())
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["API_TOKEN"] = SecretValue },
        };

        var text = request.ToString();

        text.ShouldContain("API_TOKEN", Case.Sensitive);
        text.ShouldContain("/tmp/project", Case.Sensitive);
        text.ShouldNotContain(SecretValue, Case.Sensitive);
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

        public Task<ITerminalSession> StartAsync(TerminalSessionOptions options, CancellationToken cancellationToken)
        {
            Options.Add(options);
            return Task.FromResult<ITerminalSession>(new StubSession());
        }
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
