using System.Runtime.Versioning;
using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>
/// The files a hooks test needs next to the worker host: a temporary directory with the fake omp, a plugins root that holds a ready
/// <c>autonogrammer-hooks-omp</c> extension, and helpers that lay out a repository checkout with a hook file inside a session directory.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class CloudHooksFixture : IDisposable
{
    /// <summary>A project hook file whose <c>beforeShellExecution</c> hook denies every command and fails closed.</summary>
    public const string DenyHooksJson = """
        {"version":1,"hooks":{"beforeShellExecution":[{"command":"printf '{\"permission\":\"deny\",\"user_message\":\"Blocked by the project guard.\"}'","failClosed":true}]}}
        """;

    /// <summary>The request the omp extension would post before it runs a shell command.</summary>
    public const string ShellRequest = """
        {"event":"beforeShellExecution","kind":"cursor","toolCallId":"call-1","payload":{"command":"rm -rf /","cwd":"/work","sandbox":false}}
        """;

    /// <summary>Initializes a new instance of the <see cref="CloudHooksFixture"/> class.</summary>
    public CloudHooksFixture()
    {
        LinuxOnly.Require();
        Directory = System.IO.Directory.CreateTempSubdirectory("autonogrammer-worker-hooks-").FullName;
        OmpPath = HooksFakeOmpScript.Write(Directory);
        PluginsRoot = Path.Combine(Directory, "plugins");
        var source = Path.Combine(PluginsRoot, "autonogrammer-hooks-omp", "src");
        System.IO.Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(PluginsRoot, "autonogrammer-hooks-omp", "package.json"), "{\"name\":\"autonogrammer-hooks-omp\",\"omp\":{\"extensions\":[\"./src/index.mjs\"]}}");
        File.WriteAllText(Path.Combine(source, "index.mjs"), "export default function () {}\n");
        HooksFile = Path.Combine(Directory, "hooks-env.txt");
        ArgvFile = Path.Combine(Directory, "argv.txt");
    }

    /// <summary>Gets the temporary directory.</summary>
    public string Directory { get; }

    /// <summary>Gets the absolute path of the fake omp.</summary>
    public string OmpPath { get; }

    /// <summary>Gets the plugins root with the hooks extension.</summary>
    public string PluginsRoot { get; }

    /// <summary>Gets the file the fake omp writes the bridge URL and token to.</summary>
    public string HooksFile { get; }

    /// <summary>Gets the file the fake omp writes its arguments to.</summary>
    public string ArgvFile { get; }

    /// <summary>Gets the omp arguments that make the fake write its facts to the fixture's files.</summary>
    public IReadOnlyList<string> OmpArguments => ["--hooks-file", HooksFile, "--argv-file", ArgvFile];

    /// <summary>Gets the configuration that turns hooks on and points the worker at the fake omp and the extension.</summary>
    /// <param name="enabled">Whether hooks are enabled.</param>
    /// <returns>The configuration overrides for <see cref="WorkerHost.StartAsync"/>.</returns>
    public IReadOnlyDictionary<string, string?> Settings(bool enabled = true) => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["Worker:Omp:Path"] = OmpPath,
        ["Worker:Hooks:Enabled"] = enabled ? "true" : "false",
        ["Worker:Hooks:PluginsRoot"] = PluginsRoot,
    };

    /// <summary>Lays out a checkout with a project hook file inside a session directory.</summary>
    /// <param name="workspaceRoot">The worker's workspace root.</param>
    /// <param name="session">The session id.</param>
    /// <param name="hooksJson">The content of <c>.cursor/hooks.json</c>, or null for none.</param>
    /// <returns>The checkout directory.</returns>
    public static string WriteCheckout(string workspaceRoot, string session, string? hooksJson)
    {
        var repository = Path.Combine(workspaceRoot, session, "repo");
        System.IO.Directory.CreateDirectory(Path.Combine(repository, ".git"));
        if (hooksJson is not null)
        {
            System.IO.Directory.CreateDirectory(Path.Combine(repository, ".cursor"));
            File.WriteAllText(Path.Combine(repository, ".cursor", "hooks.json"), hooksJson);
        }

        return repository;
    }

    /// <summary>Computes the consent identities of the project hooks of a checkout, as the desktop would.</summary>
    /// <param name="repository">The checkout directory.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The identities of every project hook.</returns>
    public static async Task<string[]> HashesAsync(string repository, CancellationToken cancellationToken)
    {
        var context = new HookLocatorContext([repository], HomeDirectory: null, HookPlatforms.Current, IsRemote: true);
        var set = await new HooksConfigLoader(new LocalHookFileSource()).LoadAsync(context, null, cancellationToken).ConfigureAwait(true);
        return [.. set.Hooks.Where(static hook => hook.Scope == HookScope.Project).Select(static hook => hook.CommandHash)];
    }

    /// <summary>Reads the bridge URL and token the fake omp was given.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The URL and the token; empty strings when the child had no bridge.</returns>
    public async Task<(string Url, string Token)> ReadBridgeAsync(CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(HooksFile, cancellationToken).ConfigureAwait(true);
        return (lines[0], lines[1]);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A temporary directory that cannot be removed does not fail the test.
            _ = ex;
        }
    }
}
