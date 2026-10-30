using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>scripts/render-web-config.sh writes Auth:Origin only from a valid https origin and never touches the Demo file.</summary>
public sealed class RenderWebConfigScriptTests
{
    /// <summary>A valid https origin is written. An unset variable leaves Origin empty.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AValidAuthOriginIsWrittenAndAnUnsetOriginStaysEmpty()
    {
        var written = await RunAsync("https://auth.autonogrammer.example").ConfigureAwait(true);
        written.ExitCode.ShouldBe(0);
        written.Text.ShouldContain("\"Origin\": \"https://auth.autonogrammer.example\"");

        var empty = await RunAsync(null).ConfigureAwait(true);
        empty.ExitCode.ShouldBe(0);
        empty.Text.ShouldContain("\"Origin\": \"\"");
        empty.Text.ShouldNotContain("auth.autonogrammer.example");
    }

    /// <summary>http, a wildcard, a path and userinfo are rejected and never echoed.</summary>
    /// <param name="origin">The rejected origin.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData("http://auth.autonogrammer.example")]
    [InlineData("https://*.autonogrammer.example")]
    [InlineData("https://auth.autonogrammer.example/path")]
    [InlineData("https://user:pass@auth.autonogrammer.example")]
    public async Task AnInvalidAuthOriginIsRejectedWithoutEchoingIt(string origin)
    {
        var result = await RunAsync(origin).ConfigureAwait(true);

        result.ExitCode.ShouldNotBe(0);
        result.StandardError.Contains(origin, StringComparison.Ordinal).ShouldBeFalse();
        result.Text.ShouldNotContain(origin);
    }

    /// <summary>The Demo settings file is not given an auth origin by this plan.</summary>
    [Fact]
    public void TheDemoSettingsFileHasNoAuthOrigin()
    {
        var root = RepoRoot();
        var demo = File.ReadAllText(Path.Combine(root.FullName, "apps", "Autonogrammer.Web", "wwwroot", "appsettings.demo.json"));
        demo.ShouldNotContain("Origin");
        demo.ShouldNotContain("AUTONOGRAMMER_AUTH_ORIGIN");
    }

    private static async Task<ScriptResult> RunAsync(string? authOrigin)
    {
        var root = RepoRoot();
        var script = Path.Combine(root.FullName, "scripts", "render-web-config.sh");
        var settings = Path.Combine(Path.GetTempPath(), "ag-appsettings-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(settings, """
            {
              "Api": { "BaseUrl": "" },
              "Auth": { "Origin": "" }
            }
            """).ConfigureAwait(true);
        try
        {
            var start = new ProcessStartInfo("sh")
            {
                WorkingDirectory = root.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(script);
            start.ArgumentList.Add(settings);
            start.Environment["AUTONOGRAMMER_API_ORIGIN"] = "https://api.autonogrammer.example";
            if (authOrigin is null)
            {
                start.Environment.Remove("AUTONOGRAMMER_AUTH_ORIGIN");
            }
            else
            {
                start.Environment["AUTONOGRAMMER_AUTH_ORIGIN"] = authOrigin;
            }

            using var process = Process.Start(start) ?? throw new InvalidOperationException("sh did not start.");
            var stdout = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            var stderr = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            var text = await File.ReadAllTextAsync(settings, TestContext.Current.CancellationToken).ConfigureAwait(true);
            return new ScriptResult(process.ExitCode, stdout, stderr, text);
        }
        finally
        {
            File.Delete(settings);
        }
    }

    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull();
        return dir;
    }

    private sealed record ScriptResult(int ExitCode, string StandardOutput, string StandardError, string Text);
}
