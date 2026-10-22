using Shouldly;
using Xunit;

namespace OmpOS.Harness.OmpLauncher.Tests;

public sealed class OmpVersionProbeTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly FakeOmp _fake = new();

    /// <inheritdoc />
    public void Dispose() => _fake.Dispose();

    [Fact]
    public async Task ReportsTheFakeOutputForTheSingleVersionArgument()
    {
        var directory = _fake.CreateDirectory("probe");
        var path = _fake.WriteExecutable(directory, """
            if [ "$#" -eq 1 ] && [ "$1" = version ]; then
              echo "omp 9.8.7"
              exit 0
            fi
            echo "unexpected arguments: $*" >&2
            exit 2
            """);

        var result = await OmpVersionProbe.ProbeAsync(path, Token);

        result.ShouldBe(new OmpVersionReported("omp 9.8.7"));
    }

    [Fact]
    public async Task ReportsNonZeroExitWithItsCodeAndStandardError()
    {
        var directory = _fake.CreateDirectory("probe");
        var path = _fake.WriteExecutable(directory, "echo boom >&2\nexit 4");

        var result = await OmpVersionProbe.ProbeAsync(path, Token);

        result.ShouldBe(new OmpVersionProbeFailed(OmpProbeFailure.NonZeroExit, 4, "boom"));
    }

    [Fact]
    public async Task ReportsSuccessfulExitWithNoOutputAsEmptyOutput()
    {
        var directory = _fake.CreateDirectory("probe");
        var path = _fake.WriteExecutable(directory, "exit 0");

        var result = await OmpVersionProbe.ProbeAsync(path, Token);

        result.ShouldBeOfType<OmpVersionProbeFailed>().Failure.ShouldBe(OmpProbeFailure.EmptyOutput);
    }

    [Fact]
    public async Task ReportsMissingExecutableAsStartFailureNotAsNotFound()
    {
        var missing = Path.Combine(_fake.Root, "absent", "omp");

        var result = await OmpVersionProbe.ProbeAsync(missing, Token);

        var failed = result.ShouldBeOfType<OmpVersionProbeFailed>();
        failed.Failure.ShouldBe(OmpProbeFailure.StartFailed);
        failed.ExitCode.ShouldBeNull();
    }

    [Fact]
    public async Task KillsTheProbeTreeWhenTheTimeoutPasses()
    {
        var directory = _fake.CreateDirectory("probe");
        var pidFile = Path.Combine(_fake.Root, "pids");
        var path = _fake.WriteExecutable(directory, $"sleep 30 &\necho \"$$ $!\" > '{pidFile}'\nwait");

        var result = await OmpVersionProbe.ProbeAsync(path, Token, TimeSpan.FromSeconds(2));

        result.ShouldBeOfType<OmpVersionProbeFailed>().Failure.ShouldBe(OmpProbeFailure.TimedOut);
        var pids = (await File.ReadAllTextAsync(pidFile, Token)).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray();
        pids.Length.ShouldBe(2);
        foreach (var pid in pids)
        {
            (await WaitUntilDeadAsync(pid, Token)).ShouldBeTrue($"process {pid} survived the timeout kill");
        }
    }

    [Fact]
    public async Task RejectsANonPositiveTimeout()
    {
        var directory = _fake.CreateDirectory("probe");
        var path = _fake.WriteExecutable(directory, "exit 0");

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            OmpVersionProbe.ProbeAsync(path, Token, TimeSpan.Zero));
    }

    [Fact]
    public void DefaultTimeoutIsTenSeconds()
    {
        OmpVersionProbe.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(10));
    }

    // Linux reports a reaped process as absent and a zombie as state Z. Either means it is no longer running.
    private static async Task<bool> WaitUntilDeadAsync(int pid, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (!IsRunning(pid))
            {
                return true;
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        return !IsRunning(pid);
    }

    private static bool IsRunning(int pid)
    {
        var statPath = $"/proc/{pid}/stat";
        if (!File.Exists(statPath))
        {
            return false;
        }

        try
        {
            var stat = File.ReadAllText(statPath);
            var state = stat[stat.LastIndexOf(')') + 2];
            return state is not 'Z' and not 'X';
        }
        catch (IOException)
        {
            return false;
        }
    }
}
