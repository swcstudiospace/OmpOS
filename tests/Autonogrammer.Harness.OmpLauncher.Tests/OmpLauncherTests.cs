using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpLauncher.Tests;

public sealed class OmpLauncherTests : IDisposable
{
    private const string SecretName = "AUTONOGRAMMER_LAUNCHER_TEST_SECRET";
    private const string SecretValue = "must-not-reach-omp";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly FakeOmp _fake = new();

    /// <inheritdoc />
    public void Dispose() => _fake.Dispose();

    [Fact]
    public async Task PassesTheArgumentListAndWorkingDirectoryExactly()
    {
        var workingDirectory = _fake.CreateDirectory("work dir with spaces");
        var path = _fake.WriteExecutable(_fake.CreateDirectory("bin"), """
            pwd -P
            printf '%s\n' "$@"
            """);
        var arguments = new[]
        {
            "--mode",
            "two words",
            "",
            "a;b",
            "$(echo injected)",
            "`echo hi`",
            "*",
            "--flag=x y",
        };

        var session = LaunchedSession(new OmpLaunchRequest(path, arguments, workingDirectory));
        using (session)
        {
            var output = await session.StandardOutput.ReadToEndAsync(Token);
            (await session.WaitForExitAsync(Token)).ShouldBe(0);

            var expected = Path.GetFullPath(workingDirectory) + "\n" + string.Concat(arguments.Select(argument => argument + "\n"));
            output.ShouldBe(expected);
        }
    }

    [Fact]
    public async Task ChildEnvironmentContainsOnlyTheAllowlist()
    {
        var path = _fake.WriteExecutable(_fake.CreateDirectory("bin"), "env");
        var workingDirectory = _fake.CreateDirectory("work");
        Environment.SetEnvironmentVariable(SecretName, SecretValue);
        try
        {
            var session = LaunchedSession(new OmpLaunchRequest(path, [], workingDirectory));
            using (session)
            {
                var output = await session.StandardOutput.ReadToEndAsync(Token);
                (await session.WaitForExitAsync(Token)).ShouldBe(0);

                // The shell adds these on its own; they are not inherited from the parent.
                var shellAdded = new HashSet<string>(StringComparer.Ordinal) { "PWD", "OLDPWD", "SHLVL", "_" };
                var names = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.Contains('=', StringComparison.Ordinal))
                    .Select(line => line[..line.IndexOf('=', StringComparison.Ordinal)])
                    .Where(name => !shellAdded.Contains(name))
                    .ToHashSet(StringComparer.Ordinal);

                names.Contains(SecretName, StringComparer.Ordinal).ShouldBeFalse();
                names.Except(OmpProcessStarter.AllowedEnvironmentVariables, StringComparer.Ordinal).ShouldBeEmpty();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretName, null);
        }
    }

    [Fact]
    public void MissingWorkingDirectoryIsAFailureNotAnException()
    {
        var path = _fake.WriteExecutable(_fake.CreateDirectory("bin"), "exit 0");
        var missing = Path.Combine(_fake.Root, "absent");

        var result = OmpProcessStarter.Start(new OmpLaunchRequest(path, [], missing));

        result.ShouldBeOfType<OmpLaunchFailed>().Reason.ShouldContain(missing);
    }

    [Fact]
    public void MissingExecutableIsAFailureNotAnException()
    {
        var workingDirectory = _fake.CreateDirectory("work");
        var missing = Path.Combine(_fake.Root, "absent", "omp");

        var result = OmpProcessStarter.Start(new OmpLaunchRequest(missing, [], workingDirectory));

        result.ShouldBeOfType<OmpLaunchFailed>();
    }

    [Fact]
    public void RelativeExecutablePathIsRejectedBeforeStart()
    {
        var workingDirectory = _fake.CreateDirectory("work");

        var result = OmpProcessStarter.Start(new OmpLaunchRequest("omp", [], workingDirectory));

        result.ShouldBeOfType<OmpLaunchFailed>();
    }

    [Fact]
    public async Task KillStopsARunningSession()
    {
        var path = _fake.WriteExecutable(_fake.CreateDirectory("bin"), "sleep 30");
        var session = LaunchedSession(new OmpLaunchRequest(path, [], _fake.CreateDirectory("work")));
        using (session)
        {
            session.Kill();

            var exitCode = await session.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(10), Token);

            exitCode.ShouldNotBe(0);
        }
    }

    private static OmpSession LaunchedSession(OmpLaunchRequest request)
    {
        var result = OmpProcessStarter.Start(request);
        return result.ShouldBeOfType<OmpLaunchStarted>().Session;
    }
}
