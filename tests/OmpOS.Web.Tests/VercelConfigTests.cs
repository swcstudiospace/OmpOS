using System.Text.Json;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>vercel.json validity: parses, serves the SPA, carries security headers, never uses legacy routes.</summary>
public sealed class VercelConfigTests
{
    private static readonly string[] NoneOnly = ["'none'"];
    private static readonly string[] BroadSourceDirectives = ["default-src", "connect-src", "img-src"];
    private const string SelfSource = "'self'";
    private static readonly string[] LoopbackHosts = ["127.0.0.1", "localhost"];

    /// <summary>The deploy config parses with rewrites and headers but no legacy routes key.</summary>
    [Fact]
    public void VercelConfigHasRewritesAndHeadersWithoutRoutes()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot().FullName, "vercel.json")));
        var root = doc.RootElement;

        root.TryGetProperty("routes", out _).ShouldBeFalse("routes must never mix with rewrites/headers");
        root.GetProperty("outputDirectory").GetString().ShouldBe("output/wwwroot");

        var rewrites = root.GetProperty("rewrites").EnumerateArray().ToArray();
        rewrites.ShouldNotBeEmpty();
        rewrites.Any(static r =>
            string.Equals(r.GetProperty("source").GetString(), "/(.*)", StringComparison.Ordinal) &&
            string.Equals(r.GetProperty("destination").GetString(), "/index.html", StringComparison.Ordinal)).ShouldBeTrue();

        var headers = root.GetProperty("headers").EnumerateArray().ToArray();
        headers.ShouldNotBeEmpty();
        var global = headers.Single(h =>
            string.Equals(h.GetProperty("source").GetString(), "/(.*)", StringComparison.Ordinal));
        var byKey = global.GetProperty("headers").EnumerateArray()
            .ToDictionary(
                static h => h.GetProperty("key").GetString()!,
                static h => h.GetProperty("value").GetString()!,
                StringComparer.Ordinal);

        byKey["X-Content-Type-Options"].ShouldBe("nosniff");
        byKey["Referrer-Policy"].ShouldBe("strict-origin-when-cross-origin");
        byKey["X-Frame-Options"].ShouldBe("DENY");
        var csp = ParseCsp(byKey["Content-Security-Policy"]);
        csp["script-src"].Contains("'self'", StringComparer.Ordinal).ShouldBeTrue();
        csp["script-src"].Contains("'wasm-unsafe-eval'", StringComparer.Ordinal).ShouldBeTrue();
        csp["script-src"].Contains("'unsafe-eval'", StringComparer.Ordinal).ShouldBeFalse();
        csp["object-src"].ShouldBe(NoneOnly);
        csp["frame-ancestors"].ShouldBe(NoneOnly);
        // Agent output can name any host, so the browser may fetch only same-origin resources and inline raster data.
        foreach (var source in csp["img-src"])
        {
            (source is "'self'" or "data:").ShouldBeTrue($"img-src must not admit a remote source: {source}");
        }
        foreach (var directive in BroadSourceDirectives)
        {
            csp[directive].Contains("*", StringComparer.Ordinal).ShouldBeFalse();
            csp[directive].Contains("https:", StringComparer.Ordinal).ShouldBeFalse();
            csp[directive].Contains("http:", StringComparer.Ordinal).ShouldBeFalse();
        }
    }

    /// <summary>connect-src admits only 'self' and loopback http origins, never a wide origin.</summary>
    [Fact]
    public void VercelConfigConnectSrcAllowsOnlySelfAndLoopbackHttp()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot().FullName, "vercel.json")));
        var global = doc.RootElement.GetProperty("headers").EnumerateArray()
            .Single(static h =>
                string.Equals(h.GetProperty("source").GetString(), "/(.*)", StringComparison.Ordinal));
        var byKey = global.GetProperty("headers").EnumerateArray()
            .ToDictionary(
                static h => h.GetProperty("key").GetString()!,
                static h => h.GetProperty("value").GetString()!,
                StringComparer.Ordinal);
        var connectSrc = ParseCsp(byKey["Content-Security-Policy"])["connect-src"];
        connectSrc.ShouldNotBeEmpty();
        foreach (var source in connectSrc)
        {
            if (string.Equals(source, SelfSource, StringComparison.Ordinal))
            {
                continue;
            }

            var isLoopbackHttp = Uri.TryCreate(source, UriKind.Absolute, out var uri)
                && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                && LoopbackHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
            isLoopbackHttp.ShouldBeTrue($"connect-src must not admit a non-loopback source: {source}");
        }
    }

    /// <summary>The build command publishes the Web head through the pinned SDK path.</summary>
    [Fact]
    public void VercelConfigBuildUsesPinnedSdkAndWebProject()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot().FullName, "vercel.json")));
        var root = doc.RootElement;

        root.GetProperty("framework").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("installCommand").GetString().ShouldNotBeNull().ShouldContain("global.json");
        var build = root.GetProperty("buildCommand").GetString().ShouldNotBeNull();
        build.ShouldContain("./.dotnet/dotnet publish");
        build.ShouldContain("apps/OmpOS.Web");
    }

    private static Dictionary<string, string[]> ParseCsp(string policy) =>
        policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static directive => directive.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(static parts => parts[0], static parts => parts[1..], StringComparer.Ordinal);

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
}
