using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Autonogrammer.Harness.OmpLauncher.Tests;

/// <summary>
/// A managed root, a fake registry with a fake Bun release, and fake processes. Nothing touches the network, and no real Bun or
/// omp is ever started.
/// </summary>
internal sealed class OmpTestHost : IDisposable
{
    public const string PlatformKey = "linux-x64";
    public const string BunVersion = "1.4.4";
    public const string PackageUrl = "https://registry.test/@oh-my-pi/pi-coding-agent";
    public const string BunUrl = "https://github.test/oven-sh/bun/releases/download/bun-v1.4.4/bun-linux-x64.zip";

    private const string RegistryBase = "https://registry.test/";
    private const string GitHubBase = "https://github.test/";

    private readonly HttpClient _client;
    private readonly Dictionary<string, PublishedRelease> _published = new(StringComparer.Ordinal);
    private string _latest = string.Empty;

    public OmpTestHost()
    {
        Root = Path.Combine(Path.GetTempPath(), "autonogrammer-omp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Root);
        Registry = new FakeRegistry();
        _client = new HttpClient(Registry);
        Runner = new FakeProcessRunner(Respond);
        FileSystem = new FakeFileSystem();
        Clock = new FixedClock(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
        BunArchive = TarballBuilder.BunArchive("bun-linux-x64/bun", Encoding.UTF8.GetBytes("fake bun"));
        Pin = new OmpBunPin(
            BunVersion,
            new Dictionary<string, OmpBunAsset>(StringComparer.Ordinal)
            {
                [PlatformKey] = new("bun-linux-x64.zip", Hex(BunArchive)),
            });
        Registry.Route(BunUrl, () => Bytes(BunArchive));
        Registry.Route(PackageUrl, () => Json(PackumentJson()));
    }

    public string Root { get; }

    public FakeRegistry Registry { get; }

    public FakeProcessRunner Runner { get; }

    public FakeFileSystem FileSystem { get; }

    public FixedClock Clock { get; }

    public OmpBunPin Pin { get; }

    public byte[] BunArchive { get; }

    /// <summary>When set, the health probe of this version's activated executable exits with an error.</summary>
    public string? FailActivationOf { get; set; }

    public string StatePath => Path.Combine(Root, "omp", "state.json");

    public string StagingDirectory => Path.Combine(Root, "omp", "staging");

    public string DownloadsDirectory => Path.Combine(Root, "omp", "downloads");

    public string VersionDirectory(string version) => Path.Combine(Root, "omp", "versions", version);

    public string ExecutablePath(string version) => Path.Combine(VersionDirectory(version), "bin", OmpLocator.ExecutableName);

    public OmpManagedOptions Options => new(
        Root,
        new Uri(RegistryBase),
        new Uri(GitHubBase),
        _client,
        Runner,
        FileSystem,
        Clock)
    {
        BunPin = Pin,
        PlatformKey = PlatformKey,
    };

    public OmpManagedInstaller Installer() => new(Options);

    public OmpUpdateCheck UpdateCheck() => new(Installer());

    public OmpManagedStateStore Store() => new(StatePath);

    /// <summary>Publishes a version as the registry's latest.</summary>
    /// <param name="version">The version.</param>
    /// <param name="tarball">The bytes the tarball address serves.</param>
    /// <param name="engines">The engines.bun range to publish, or null for none.</param>
    /// <param name="integritySource">Bytes the published sha512 is computed from. Null uses <paramref name="tarball"/>.</param>
    /// <param name="declaredLength">A Content-Length that differs from the body, or null for the body's own length.</param>
    public void Publish(string version, byte[] tarball, string? engines = ">=1.4.4", byte[]? integritySource = null, long? declaredLength = null)
    {
        var integrity = "sha512-" + Convert.ToBase64String(SHA512.HashData(integritySource ?? tarball));
        _published[version] = new PublishedRelease(integrity, engines);
        _latest = version;
        Registry.Route(TarballUrl(version), () => Tarball(tarball, declaredLength));
    }

    /// <summary>Writes an executable for <paramref name="version"/> and records it as the active install.</summary>
    /// <param name="version">The active version.</param>
    /// <param name="lastChecked">The recorded last-check time, or null.</param>
    /// <returns>The recorded SHA-256 of the executable.</returns>
    public string SeedActive(string version, DateTimeOffset? lastChecked)
    {
        var sha = WriteExecutable(version);
        Store().Save(new OmpManagedState(version, sha, null, null, lastChecked));
        return sha;
    }

    /// <summary>Writes an executable for <paramref name="version"/> without recording it.</summary>
    /// <param name="version">The version.</param>
    /// <returns>The SHA-256 of the written executable.</returns>
    public string WriteExecutable(string version)
    {
        var executable = ExecutablePath(version);
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, "#!fake omp " + version + "\n");
        return Sha256Of(executable);
    }

    public static string Sha256Of(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static string TarballUrl(string version) => "https://registry.test/@oh-my-pi/pi-coding-agent/-/pi-coding-agent-" + version + ".tgz";

    private static string Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private static HttpResponseMessage Tarball(byte[] bytes, long? declaredLength)
    {
        var response = Bytes(bytes);
        if (declaredLength is { } length)
        {
            response.Content.Headers.ContentLength = length;
        }

        return response;
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private string PackumentJson()
    {
        var versions = new JsonObject();
        foreach (var (version, published) in _published)
        {
            var manifest = new JsonObject
            {
                ["version"] = version,
                ["dist"] = new JsonObject
                {
                    ["tarball"] = TarballUrl(version),
                    ["integrity"] = published.Integrity,
                },
            };
            if (published.Engines is not null)
            {
                manifest["engines"] = new JsonObject { ["bun"] = published.Engines };
            }

            versions[version] = manifest;
        }

        var root = new JsonObject
        {
            ["dist-tags"] = new JsonObject { ["latest"] = _latest },
            ["versions"] = versions,
        };
        return root.ToJsonString();
    }

    private OmpProcessResult Respond(OmpProcessRequest request)
    {
        var first = request.Arguments.Count > 0 ? request.Arguments[0] : string.Empty;
        if (string.Equals(first, "install", StringComparison.Ordinal))
        {
            return new OmpProcessResult(OmpProcessOutcome.Exited, 0, "installed", null);
        }

        if (string.Equals(first, "build", StringComparison.Ordinal))
        {
            var outfile = request.Arguments[request.Arguments.Count - 1];
            File.WriteAllText(outfile, "#!fake omp\n");
            return new OmpProcessResult(OmpProcessOutcome.Exited, 0, "compiled", null);
        }

        if (request.Arguments.Count == 1 && string.Equals(first, "--version", StringComparison.Ordinal))
        {
            if (FailActivationOf is not null
                && request.FileName.Contains(Path.Combine("versions", FailActivationOf) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return new OmpProcessResult(OmpProcessOutcome.Exited, 1, "boom", null);
            }

            return new OmpProcessResult(OmpProcessOutcome.Exited, 0, "omp " + VersionOf(request.FileName), null);
        }

        return new OmpProcessResult(OmpProcessOutcome.Exited, 2, "unexpected command", null);
    }

    private static string VersionOf(string fileName)
    {
        var parts = fileName.Split(Path.DirectorySeparatorChar);
        for (var index = 0; index + 1 < parts.Length; index++)
        {
            if (string.Equals(parts[index], "versions", StringComparison.Ordinal))
            {
                return parts[index + 1];
            }

            if (string.Equals(parts[index], "staging", StringComparison.Ordinal))
            {
                return parts[index + 1].Split('-')[0];
            }
        }

        return "unknown";
    }

    private sealed record PublishedRelease(string Integrity, string? Engines);
}
