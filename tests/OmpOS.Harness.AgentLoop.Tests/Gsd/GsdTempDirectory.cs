namespace OmpOS.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// A temporary directory that is deleted when disposed. It holds fake workspaces, a fake tool install and stand-in scripts.
/// </summary>
internal sealed class GsdTempDirectory : IDisposable
{
    /// <summary>Creates an empty directory under the system temporary directory.</summary>
    public GsdTempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ompos-agentloop-gsd-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the directory path.</summary>
    public string Path { get; }

    /// <summary>Returns the path of an entry in this directory.</summary>
    /// <param name="name">The entry name or relative path.</param>
    public string PathOf(string name) => System.IO.Path.Combine(Path, name);

    /// <summary>Creates a workspace directory with <c>.planning/ROADMAP.md</c> and <c>.planning/STATE.md</c>.</summary>
    /// <param name="name">The workspace directory name.</param>
    /// <returns>The workspace root.</returns>
    public string CreateWorkspace(string name)
    {
        var root = PathOf(name);
        Directory.CreateDirectory(System.IO.Path.Combine(root, ".planning"));
        File.WriteAllText(System.IO.Path.Combine(root, ".planning", "ROADMAP.md"), "# Roadmap\n");
        File.WriteAllText(System.IO.Path.Combine(root, ".planning", "STATE.md"), "# State\n");
        return root;
    }

    /// <summary>Creates a workspace directory without a <c>.planning</c> directory.</summary>
    /// <param name="name">The workspace directory name.</param>
    /// <returns>The workspace root.</returns>
    public string CreateWorkspaceWithoutPlanning(string name)
    {
        var root = PathOf(name);
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>Creates a fake tool install holding <c>bin/gsd-tools.cjs</c>.</summary>
    /// <param name="name">The install directory name.</param>
    /// <returns>The path of the script.</returns>
    public string CreateToolsHome(string name)
    {
        var bin = PathOf(System.IO.Path.Combine(name, "bin"));
        Directory.CreateDirectory(bin);
        var script = System.IO.Path.Combine(bin, "gsd-tools.cjs");
        File.WriteAllText(script, "// stand-in\n");
        return script;
    }

    /// <summary>Replaces a planning file with different content, which changes its length.</summary>
    /// <param name="workspaceRoot">The workspace root.</param>
    /// <param name="fileName">The file name inside <c>.planning</c>.</param>
    /// <param name="content">The new content.</param>
    public static void RewritePlanningFile(string workspaceRoot, string fileName, string content) =>
        File.WriteAllText(System.IO.Path.Combine(workspaceRoot, ".planning", fileName), content);

    /// <summary>Deletes the directory and everything in it.</summary>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
