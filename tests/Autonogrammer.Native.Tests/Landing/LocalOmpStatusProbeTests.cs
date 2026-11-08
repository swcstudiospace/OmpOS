using System.Runtime.Versioning;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Native.Composition;
using Autonogrammer.Presentation.Landing;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Landing;

/// <summary>The omp status the Code landing shows, and the omp a launch resolves: which source supplied the executable, and what the probe reported. The directories and paths are the test's own.</summary>
public sealed class LocalOmpStatusProbeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "landing-omp-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Removes the directory the test wrote.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>An omp at the configured absolute path is reported as the configured source, with the version it prints.</summary>
    [Fact]
    public async Task AConfiguredPathIsTheConfiguredSource()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var omp = WriteOmp(Path.Combine(_root, "configured", "omp"), "1.2.3");
        var probe = new LocalOmpStatusProbe(omp, managed: null, Empty());

        var status = await probe.ReadAsync(Token);

        status.ShouldBe(LocalOmpStatus.Installed("1.2.3", LocalOmpSource.Configured));
    }

    /// <summary>An omp found in a PATH directory is reported as the PATH source.</summary>
    [Fact]
    public async Task AnOmpOnPathIsThePathSource()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        WriteOmp(Path.Combine(_root, "bin", "omp"), "2.0.0");
        var probe = new LocalOmpStatusProbe(configuredPath: null, managed: null, new OmpLocatorOptions(null, [Path.Combine(_root, "bin")], []));

        var status = await probe.ReadAsync(Token);

        status.ShouldBe(LocalOmpStatus.Installed("2.0.0", LocalOmpSource.Path));
    }

    /// <summary>A configured path that does not run falls back to PATH, and the status says PATH.</summary>
    [Fact]
    public async Task AnUnrunnableConfiguredPathFallsBackToPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        WriteOmp(Path.Combine(_root, "bin", "omp"), "3.0.0");
        var probe = new LocalOmpStatusProbe(
            Path.Combine(_root, "missing", "omp"),
            managed: null,
            new OmpLocatorOptions(null, [Path.Combine(_root, "bin")], []));

        var status = await probe.ReadAsync(Token);

        status.ShouldBe(LocalOmpStatus.Installed("3.0.0", LocalOmpSource.Path));
    }

    /// <summary>With no runnable omp the status says it is not installed and carries the install guidance.</summary>
    [Fact]
    public async Task NothingRunnableIsNotInstalledWithTheGuidance()
    {
        var probe = new LocalOmpStatusProbe(configuredPath: null, managed: null, Empty());

        var status = await probe.ReadAsync(Token);

        status.State.ShouldBe(LocalOmpState.NotInstalled);
        status.Detail.ShouldBe(OmpLocator.InstallGuidance);
    }

    /// <summary>A launch runs the configured path when it is runnable: the resolution names that executable.</summary>
    [Fact]
    public async Task ALaunchResolvesTheConfiguredExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var omp = WriteOmp(Path.Combine(_root, "configured", "omp"), "1.2.3");
        var probe = new LocalOmpStatusProbe(omp, managed: null, Empty());

        var executable = await probe.ResolveExecutableAsync(Token);

        executable.ShouldBe(omp);
    }

    /// <summary>A launch with nothing runnable resolves to null, so the session shows the install guidance instead of starting.</summary>
    [Fact]
    public async Task ALaunchWithNothingRunnableResolvesToNull()
    {
        var probe = new LocalOmpStatusProbe(configuredPath: null, managed: null, Empty());

        var executable = await probe.ResolveExecutableAsync(Token);

        executable.ShouldBeNull();
    }

    private static OmpLocatorOptions Empty() => new(null, [], []);

    [UnsupportedOSPlatform("windows")]
    private static string WriteOmp(string path, string version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "#!/bin/sh\necho " + version + "\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}
