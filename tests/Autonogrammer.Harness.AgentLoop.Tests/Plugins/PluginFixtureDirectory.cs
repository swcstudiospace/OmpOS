using System.Globalization;

namespace Autonogrammer.Harness.AgentLoop.Tests.Plugins;

/// <summary>
/// A temporary directory that is deleted when disposed, with helpers that lay out plugin trees the way the submodules do.
/// </summary>
internal sealed class PluginFixtureDirectory : IDisposable
{
    /// <summary>Creates an empty directory under the system temporary directory.</summary>
    public PluginFixtureDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "autonogrammer-agentloop-tests",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Path);
    }

    /// <summary>The directory path.</summary>
    public string Path { get; }

    /// <summary>The plugins root inside the directory.</summary>
    public string PluginsRoot => PathOf("plugins");

    /// <summary>Returns a path below the directory.</summary>
    /// <param name="relative">The path relative to the directory, with <c>/</c> separators.</param>
    public string PathOf(string relative) =>
        System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    /// <summary>Writes a text file below the directory, creating directories as needed.</summary>
    /// <param name="relative">The path relative to the directory.</param>
    /// <param name="content">The text.</param>
    public string WriteFile(string relative, string content = "// fixture\n")
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Writes a package.json that declares extensions under the given key.</summary>
    /// <param name="relativeDirectory">The plugin directory relative to the directory.</param>
    /// <param name="key">The section name, <c>omp</c> or <c>pi</c>.</param>
    /// <param name="extensions">The declared extension paths.</param>
    public void WritePackage(string relativeDirectory, string key, params string[] extensions)
    {
        var list = string.Join(", ", extensions.Select(extension => "\"" + extension + "\""));
        WriteFile(relativeDirectory + "/package.json", "{ \"name\": \"fixture\", \"" + key + "\": { \"extensions\": [" + list + "] } }");
    }

    /// <summary>Lays out all four built-in plugins so that each resolves as ready.</summary>
    public void CreateAllPlugins()
    {
        WritePackage("plugins/claude-ultrathink", "omp", "./src/host/omp.ts");
        WriteFile("plugins/claude-ultrathink/src/host/omp.ts");
        WriteFile("plugins/claude-ultrathink/.git", "gitdir: ../../.git/modules/claude-ultrathink\n");
        WriteFile("plugins/agent-swarm/.git", "gitdir: ../../.git/modules/agent-swarm\n");
        WritePackage("plugins/agent-swarm/omp", "omp", "./src/index.ts");
        WriteFile("plugins/agent-swarm/omp/src/index.ts");
        WriteFile("plugins/agent-substrate/.git", "gitdir: ../../.git/modules/agent-substrate\n");
        WriteFile("plugins/agent-substrate/packages/projector/omp/substrate-status.ts");
        WriteFile("plugins/agent-substrate/packages/projector/omp/substrate-skills.ts");
        WritePackage("plugins/gsd-core-omp", "omp", "./src/index.ts");
        WriteFile("plugins/gsd-core-omp/src/index.ts");
    }

    /// <summary>Deletes the directory and everything in it.</summary>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
