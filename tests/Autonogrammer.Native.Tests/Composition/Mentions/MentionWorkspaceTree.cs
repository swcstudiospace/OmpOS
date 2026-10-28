using System.Text;

namespace Autonogrammer.Native.Tests.Composition.Mentions;

/// <summary>A temporary directory for one test with a workspace folder and a sibling folder outside it. The whole tree is deleted on disposal.</summary>
internal sealed class MentionWorkspaceTree : IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Creates the tree: an empty workspace and an empty outside folder.</summary>
    public MentionWorkspaceTree()
    {
        Base = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "autonogrammer-mentions-" + Guid.NewGuid().ToString("N")));
        Root = Path.Combine(Base, "workspace");
        Outside = Path.Combine(Base, "outside");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Outside);
    }

    /// <summary>Gets the temporary directory that holds everything.</summary>
    public string Base { get; }

    /// <summary>Gets the workspace root.</summary>
    public string Root { get; }

    /// <summary>Gets a folder next to the workspace, which no mention may reach.</summary>
    public string Outside { get; }

    /// <summary>Writes a UTF-8 file without a byte order mark below the workspace, creating its folders.</summary>
    /// <param name="relativePath">The path below the workspace, with <c>/</c> separators.</param>
    /// <param name="content">The text.</param>
    /// <returns>The full path.</returns>
    public string Write(string relativePath, string content) => WriteBytes(relativePath, Utf8.GetBytes(content));

    /// <summary>Writes a file below the workspace, creating its folders.</summary>
    /// <param name="relativePath">The path below the workspace, with <c>/</c> separators.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The full path.</returns>
    public string WriteBytes(string relativePath, byte[] bytes)
    {
        var path = Combine(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Writes a UTF-8 file into the outside folder.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="content">The text.</param>
    /// <returns>The full path.</returns>
    public string WriteOutside(string name, string content)
    {
        var path = Path.Combine(Outside, name);
        File.WriteAllText(path, content, Utf8);
        return path;
    }

    /// <summary>Gets the full path of a path below the workspace.</summary>
    /// <param name="relativePath">The path below the workspace, with <c>/</c> separators.</param>
    /// <returns>The full path.</returns>
    public string Combine(string relativePath) => Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Base, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary folder does not fail a test.
        }
    }
}
