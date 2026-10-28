using System.Text;
using OmpOS.Harness.AgentLoop.Safety;

namespace OmpOS.Harness.AgentLoop.Tests.Safety;

/// <summary>A temporary home, workspace and outside folder on the real file system, removed on disposal.</summary>
internal sealed class SafetyRig : IDisposable
{
    public SafetyRig()
    {
        Root = Path.Combine(Path.GetTempPath(), "ompos-safety-" + Guid.NewGuid().ToString("N"));
        Home = Path.Combine(Root, "home");
        Project = Path.Combine(Root, "project");
        Outside = Path.Combine(Root, "outside");
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Project);
        Directory.CreateDirectory(Outside);
    }

    /// <summary>Gets the folder that holds everything.</summary>
    public string Root { get; }

    /// <summary>Gets the home directory.</summary>
    public string Home { get; }

    /// <summary>Gets the workspace root.</summary>
    public string Project { get; }

    /// <summary>Gets a folder beside the workspace.</summary>
    public string Outside { get; }

    /// <summary>Gets the path of a file under the workspace.</summary>
    public string InProject(string relative) => Path.Combine(Project, relative);

    /// <summary>Gets the path of a file under the home directory.</summary>
    public string InHome(string relative) => Path.Combine(Home, relative);

    /// <summary>Writes a file, creating its folder, and returns its path.</summary>
    public static string Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    /// <summary>Writes a file under the workspace.</summary>
    public string WriteProject(string relative, string text) => Write(InProject(relative), text);

    /// <summary>Writes a file under the home directory.</summary>
    public string WriteHome(string relative, string text) => Write(InHome(relative), text);

    /// <summary>Creates a symbolic link to a folder.</summary>
    public static void LinkFolder(string linkPath, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        Directory.CreateSymbolicLink(linkPath, target);
    }

    /// <summary>Loads the permissions the way a head does.</summary>
    public EffectivePermissions Permissions(bool trust = true) => PermissionsLoader.Load(Home, Project, trust);

    /// <summary>Loads the sandbox the way a head does.</summary>
    public EffectiveSandbox Sandbox(bool trust = true) => SandboxLoader.Load(Home, Project, null, trust);

    /// <summary>Builds a policy over the files written so far.</summary>
    public RunModePolicy Policy(RunMode mode, bool trust = true) => new(mode, Permissions(trust), Sandbox(trust), Project, Home);

    /// <summary>Writes the workspace's OmpOS permissions file and builds a policy.</summary>
    public RunModePolicy PolicyWith(RunMode mode, string permissionsJson, string? sandboxJson = null, bool trust = true)
    {
        WriteProject(".ompos/permissions.json", permissionsJson);
        if (sandboxJson is not null)
        {
            WriteProject(".ompos/sandbox.json", sandboxJson);
        }

        return Policy(mode, trust);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A temporary folder that cannot be removed is left to the system's temp cleaning.
        }
    }
}
