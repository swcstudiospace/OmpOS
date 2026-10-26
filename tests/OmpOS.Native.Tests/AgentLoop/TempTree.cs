using System.Text;

namespace OmpOS.Native.Tests.AgentLoop;

/// <summary>
/// A temporary directory tree for one test: a workspace, a state root and a plugins root. Nothing is created under the plugins root
/// until a test asks for it, so a test starts from "no plugins at all". The tree is deleted on disposal.
/// </summary>
internal sealed class TempTree : IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public TempTree()
    {
        Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ompos-agentloop-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Workspace);
    }

    /// <summary>Gets the root of the tree.</summary>
    public string Root { get; }

    /// <summary>Gets the plugins root. It does not exist until a test creates a plugin in it.</summary>
    public string Plugins => Path.Combine(Root, "plugins");

    /// <summary>Gets the state root. It does not exist until the head creates it.</summary>
    public string State => Path.Combine(Root, "state");

    /// <summary>Gets the workspace, which exists and holds no planning state.</summary>
    public string Workspace => Path.Combine(Root, "workspace");

    /// <summary>Writes a file under the tree, creating its directories, and returns its full path.</summary>
    /// <param name="relativePath">The path below <see cref="Root"/>, with <c>/</c> separators.</param>
    /// <param name="content">The text of the file.</param>
    /// <returns>The full path.</returns>
    public string Write(string relativePath, string content)
    {
        var path = Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Utf8);
        return path;
    }

    /// <summary>Creates a plugin package that omp can load: a directory with a <c>package.json</c> that declares one extension, and that extension.</summary>
    /// <param name="pluginsRelativeDirectory">The package directory below the plugins root, with <c>/</c> separators.</param>
    /// <param name="extension">The extension path relative to the package, for example <c>src/index.ts</c>.</param>
    public void WritePackagePlugin(string pluginsRelativeDirectory, string extension)
    {
        Write($"plugins/{pluginsRelativeDirectory}/package.json", "{\"name\":\"test-plugin\",\"omp\":{\"extensions\":[\"./" + extension + "\"]}}");
        Write($"plugins/{pluginsRelativeDirectory}/{extension}", "export default function () {}\n");
    }

    /// <summary>Creates the four built-in plugins, each ready to load. The substrate has two extension files and no package.</summary>
    public void WriteReadyPlugins()
    {
        WritePackagePlugin("claude-ultrathink", "src/host/omp.ts");
        WritePackagePlugin("agent-swarm/omp", "src/index.ts");
        Write("plugins/agent-substrate/packages/projector/omp/substrate-status.ts", "export default function () {}\n");
        Write("plugins/agent-substrate/packages/projector/omp/substrate-skills.ts", "export default function () {}\n");
        WritePackagePlugin("gsd-core-omp", "src/index.ts");
    }

    /// <summary>Gets the full path of a plugin directory below the plugins root.</summary>
    /// <param name="relativeDirectory">The directory below the plugins root, with <c>/</c> separators.</param>
    /// <returns>The full path.</returns>
    public string PluginPath(string relativeDirectory) =>
        Path.GetFullPath(Path.Combine(Plugins, relativeDirectory.Replace('/', Path.DirectorySeparatorChar)));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
