using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>
/// The offline shell never caches a credential or an API answer. The rules live in the published service worker as text, so these tests read that
/// text: a rule that is edited away, or an API route the worker does not know, fails here before it reaches a browser.
/// </summary>
public sealed partial class ServiceWorkerRulesTests
{
    /// <summary>The never-cache check refuses everything that is not a same-origin GET without credentials, a stream, a range or an auth or API path.</summary>
    [Fact]
    public void TheNeverCacheCheckCoversEveryRule()
    {
        var worker = ReadWorker();
        var check = FunctionBody(worker, "isNeverCached");

        check.ShouldContain("request.method !== \"GET\"", Case.Sensitive, "only GET is ever answered from a cache");
        check.ShouldContain("url.origin !== self.location.origin", Case.Sensitive, "a cross-origin request is never cached");
        check.ShouldContain("request.headers.has(\"Authorization\")", Case.Sensitive, "a request with a credential is never cached");
        check.ShouldContain("request.headers.has(\"Range\")", Case.Sensitive);
        check.ShouldContain("text/event-stream", Case.Sensitive, "an event stream (SSE) is never cached");
        check.ShouldContain("url.pathname === AUTH_CONFIG_PATH", Case.Sensitive);
        check.ShouldContain("url.pathname.startsWith(AUTH_PATH_PREFIX)", Case.Sensitive);
        check.ShouldContain("API_PATH_PREFIXES.some(", Case.Sensitive, "an API path is never cached");
        worker.ShouldContain("const AUTH_CONFIG_PATH = \"/auth/config\";", Case.Sensitive);
        worker.ShouldContain("const AUTH_PATH_PREFIX = \"/auth/\";", Case.Sensitive);
    }

    /// <summary>A request the check refuses reaches the handler's early return, before any respondWith.</summary>
    [Fact]
    public void TheFetchHandlerChecksTheRulesBeforeItAnswers()
    {
        var worker = ReadWorker();
        var handler = worker[worker.IndexOf("self.addEventListener(\"fetch\"", StringComparison.Ordinal)..];

        var check = handler.IndexOf("isNeverCached(request)", StringComparison.Ordinal);
        var answer = handler.IndexOf("event.respondWith", StringComparison.Ordinal);
        check.ShouldBeGreaterThan(-1);
        answer.ShouldBeGreaterThan(check);
        handler[check..answer].ShouldContain("return;", Case.Sensitive);
    }

    /// <summary>The worker writes to a cache only while installing, from the build's own file list, and never stores a response it fetched at run time.</summary>
    [Fact]
    public void TheWorkerWritesToACacheOnlyWhileInstalling()
    {
        var worker = ReadWorker();

        worker.ShouldNotContain(".put(", Case.Sensitive);
        worker.ShouldNotContain("cache.add(", Case.Sensitive);
        FunctionBody(worker, "install").ShouldContain("cache.addAll(requests)", Case.Sensitive);
        Regex.Count(worker, @"\.addAll\(", RegexOptions.None, TimeSpan.FromSeconds(2)).ShouldBe(1);
    }

    /// <summary>The cache is named for the manifest version and the activation deletes the caches of other versions.</summary>
    [Fact]
    public void CachesAreVersionedAndOldOnesAreDeleted()
    {
        var worker = ReadWorker();

        worker.ShouldContain("const CACHE_NAME = CACHE_PREFIX + self.assetsManifest.version;", Case.Sensitive);
        var activate = FunctionBody(worker, "activate");
        activate.ShouldContain("name.startsWith(CACHE_PREFIX) && name !== CACHE_NAME", Case.Sensitive);
        activate.ShouldContain("caches.delete(name)", Case.Sensitive);
    }

    /// <summary>A new worker waits; only the SKIP_WAITING message from the page, which the user sends by pressing Reload, makes it take over.</summary>
    [Fact]
    public void ANewWorkerWaitsForTheUser()
    {
        var worker = ReadWorker();
        var register = ReadWwwroot("register-service-worker.js");

        Regex.Count(worker, @"skipWaiting\(\)", RegexOptions.None, TimeSpan.FromSeconds(2)).ShouldBe(1);
        worker.ShouldContain("event.data.type === \"SKIP_WAITING\"", Case.Sensitive);
        worker.ShouldNotContain("clients.claim", Case.Sensitive);
        register.ShouldContain("type: \"SKIP_WAITING\"", Case.Sensitive);
        register.ShouldContain("role\", \"status\"", Case.Sensitive);
        register.ShouldContain("aria-live\", \"polite\"", Case.Sensitive);
        register.ShouldContain("Update available - reload", Case.Sensitive);
        register.ShouldContain("window.location.reload()", Case.Sensitive);
    }

    /// <summary>Every root the API maps is covered by a prefix the worker leaves to the network.</summary>
    [Fact]
    public void EveryApiRouteRootIsKnownToTheWorker()
    {
        var prefixes = ApiPrefixes(ReadWorker());
        var roots = ApiRouteRoots();

        roots.ShouldNotBeEmpty("the API endpoint sources were not found");
        foreach (var root in roots)
        {
            var covered = prefixes.Any(prefix => root.StartsWith(prefix, StringComparison.Ordinal)
                || (prefix.EndsWith('/') && (root + "/").StartsWith(prefix, StringComparison.Ordinal)));
            covered.ShouldBeTrue($"the API maps a route under {root}, so API_PATH_PREFIXES in service-worker.published.js must name it");
        }
    }

    /// <summary>The development worker does nothing: it neither caches nor answers.</summary>
    [Fact]
    public void TheDevelopmentWorkerDoesNothing()
    {
        var worker = ReadWwwroot("service-worker.js");

        worker.ShouldNotContain("caches", Case.Sensitive);
        worker.ShouldNotContain("respondWith", Case.Sensitive);
        worker.ShouldContain("self.addEventListener(\"fetch\", () => { });", Case.Sensitive);
    }

    /// <summary>The project swaps the published worker in under the development worker's name and has the SDK write the asset manifest; the page registers the worker from a file.</summary>
    [Fact]
    public void TheProjectAndThePageWireTheWorker()
    {
        var project = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "Autonogrammer.Web", "Autonogrammer.Web.csproj"));
        var html = ReadWwwroot("index.html");

        project.ShouldContain("<ServiceWorkerAssetsManifest>service-worker-assets.js</ServiceWorkerAssetsManifest>", Case.Sensitive);
        project.ShouldContain("<ServiceWorker Include=\"wwwroot\\service-worker.js\" PublishedContent=\"wwwroot\\service-worker.published.js\" />", Case.Sensitive);
        html.ShouldContain("<script src=\"register-service-worker.js\" defer></script>", Case.Sensitive);
        html.ShouldContain("<noscript>", Case.Sensitive);
        html.ShouldContain("role=\"status\"", Case.Sensitive);
        html.ShouldContain("Loading Autonogrammer...", Case.Sensitive);
    }

    /// <summary>The worker names the CSP directives the host has to send.</summary>
    [Fact]
    public void TheWorkerDocumentsTheCspItNeeds()
    {
        ReadWorker().ShouldContain("worker-src 'self'; manifest-src 'self'", Case.Sensitive);
    }

    private static string ReadWorker() => ReadWwwroot("service-worker.published.js");

    private static string ReadWwwroot(string file) => File.ReadAllText(Path.Combine(RepoRoot(), "apps", "Autonogrammer.Web", "wwwroot", file));

    private static string FunctionBody(string script, string name)
    {
        var start = Math.Max(script.IndexOf($"function {name}(", StringComparison.Ordinal), script.IndexOf($"async function {name}(", StringComparison.Ordinal));
        start.ShouldBeGreaterThan(-1, $"function {name} must exist in the worker");
        var end = script.IndexOf("\n}\n", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start);
        return script[start..end];
    }

    private static string[] ApiPrefixes(string worker)
    {
        var declaration = PrefixList().Match(worker);
        declaration.Success.ShouldBeTrue("API_PATH_PREFIXES must be a list of string literals");
        return [.. StringLiteral().Matches(declaration.Groups["items"].Value).Select(static match => match.Groups["value"].Value)];
    }

    private static string[] ApiRouteRoots()
    {
        var api = Path.Combine(RepoRoot(), "services", "Autonogrammer.Api");
        var roots = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(api, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match route in RouteRoot().Matches(File.ReadAllText(file)))
            {
                roots.Add(route.Groups["root"].Value);
            }
        }

        return [.. roots];
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("Could not locate the repository root from the test output directory.");
        return directory.FullName;
    }

    [GeneratedRegex("API_PATH_PREFIXES\\s*=\\s*\\[(?<items>[^\\]]*)\\]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex PrefixList();

    [GeneratedRegex("\"(?<value>[^\"]*)\"", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex StringLiteral();

    [GeneratedRegex("Map(?:Get|Post|Put|Delete|Patch|Group|Methods)\\(\"(?<root>/[^/\"{]+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RouteRoot();
}
