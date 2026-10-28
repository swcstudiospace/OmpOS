using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>Publish smoke: the Web head publishes and the static output serves the SPA shell.</summary>
public sealed class PublishSmokeTests
{
    /// <summary>Publishing the Web head yields wwwroot with index.html and the _framework payload.</summary>
    [Fact]
    public async Task PublishProducesServableWwwroot()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = RepoRoot();
        var output = Path.Combine(Path.GetTempPath(), $"autonogrammer-web-publish-{Guid.NewGuid():N}", "output");
        Directory.CreateDirectory(output);

        var startInfo = new ProcessStartInfo(
            DotnetExecutable(),
            $"publish \"{Path.Combine(root.FullName, "apps", "Autonogrammer.Web", "Autonogrammer.Web.csproj")}\" -c Release -o \"{output}\"")
        {
            WorkingDirectory = root.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["DOTNET_ROOT"] = DotnetRoot();
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        using var process = Process.Start(startInfo);
        process.ShouldNotBeNull("dotnet publish failed to start.");
        var stdout = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(true);
        var stderr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(true);
        var exited = await Task.Run(() => process.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds), ct).ConfigureAwait(true);
        exited.ShouldBeTrue($"dotnet publish timed out.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        process.ExitCode.ShouldBe(0, $"dotnet publish failed.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var wwwroot = Path.Combine(output, "wwwroot");
        File.Exists(Path.Combine(wwwroot, "index.html")).ShouldBeTrue("Publish output must contain wwwroot/index.html.");
        Directory.Exists(Path.Combine(wwwroot, "_framework")).ShouldBeTrue("Publish output must contain wwwroot/_framework/.");
        File.Exists(Path.Combine(wwwroot, "appsettings.json")).ShouldBeTrue("Publish output must contain wwwroot/appsettings.json.");

        // A production bundle holds no Demo code (ADR-0017): no Demo settings file, and no assembly in the bundle names the Demo handler.
        File.Exists(Path.Combine(wwwroot, "appsettings.demo.json")).ShouldBeFalse("A production bundle must not carry the Demo settings file.");
        var marker = System.Text.Encoding.UTF8.GetBytes("DemoApiHandler");
        foreach (var assembly in Directory.EnumerateFiles(Path.Combine(wwwroot, "_framework"), "Autonogrammer*", SearchOption.AllDirectories))
        {
            var bytes = await File.ReadAllBytesAsync(assembly, ct).ConfigureAwait(true);
            bytes.AsSpan().IndexOf(marker).ShouldBe(-1, $"{Path.GetFileName(assembly)} must not contain the Demo handler type.");
        }
    }

    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull("Could not locate the repository root from the test output directory.");
        return dir;
    }

    private static string DotnetRoot() =>
        Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "/root/.dotnet";

    private static string DotnetExecutable()
    {
        var candidate = Path.Combine(DotnetRoot(), "dotnet");
        return File.Exists(candidate) ? candidate : "dotnet";
    }
}
