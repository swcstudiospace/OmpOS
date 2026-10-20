using System.Text.Json;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>
/// Smokes against the real <c>omp</c> binary. They issue only free commands (no prompt turns) and skip when no
/// <c>omp</c> executable is on PATH.
/// </summary>
public sealed class RpcLiveSmokeTests
{
    /// <summary>Gets whether an <c>omp</c> executable is on PATH. Gates every live smoke.</summary>
    public static bool OmpOnPath => FindOnPath("omp") is not null;

    /// <summary>
    /// Spawns <c>omp --mode rpc --no-ui</c>, waits for <c>ready</c>, negotiates v2, reads <c>get_state</c>, closes
    /// stdin, and asserts a clean exit.
    /// </summary>
    [Fact(SkipUnless = nameof(OmpOnPath), Skip = "omp is not on PATH.")]
    public async Task ReadyNegotiateGetStateThenCloseStdinExitsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var process = OmpRpcProcess.Start("omp", RepoRoot());
        var session = new OmpRpcSession(process, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        try
        {
            await session.CreateAsync(ct).ConfigureAwait(true);

            session.State.ShouldBe(HarnessState.Ready);
            session.AdvertisedCapabilities.ShouldContainKey("model.id");
            JsonElement state = await session.GetStateAsync(ct).ConfigureAwait(true);
            state.TryGetProperty("model", out _).ShouldBeTrue();

            await process.CloseInputAsync(ct).ConfigureAwait(true);
            int exitCode = await process.WaitForExitAsync(ct).ConfigureAwait(true);

            exitCode.ShouldBe(0);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The version probe parses the installed release.</summary>
    [Fact(SkipUnless = nameof(OmpOnPath), Skip = "omp is not on PATH.")]
    public async Task VersionProbeParsesInstalledRelease()
    {
        var ct = TestContext.Current.CancellationToken;

        OmpVersion version = await OmpVersionProbe.ProbeAsync("omp", ct).ConfigureAwait(true);

        version.Major.ShouldBeGreaterThan(0);
        version.ToString().ShouldStartWith($"{version.Major}.{version.Minor}.");
    }

    private static string? FindOnPath(string executable)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
