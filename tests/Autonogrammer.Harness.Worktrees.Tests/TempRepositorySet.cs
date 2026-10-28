using Xunit;

namespace Autonogrammer.Worktrees.Tests;

/// <summary>
/// A temporary directory that holds several sibling git repositories (each with one commit), plain folders, and an operator worktree
/// root that is outside all of them. Disposing it deletes everything.
/// </summary>
internal sealed class TempRepositorySet : IDisposable
{
    /// <summary>
    /// Creates an empty set directory.
    /// </summary>
    public TempRepositorySet()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "autonogrammer-runset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>Gets the temporary directory that contains everything.</summary>
    public string RootPath { get; }

    /// <summary>Gets the operator-controlled directory for run worktrees. It does not exist until a worktree is created.</summary>
    public string WorktreeRoot => Path.Combine(RootPath, "worktrees");

    /// <summary>
    /// Creates a git repository with one commit on <c>main</c> at <paramref name="relativePath"/>.
    /// </summary>
    /// <param name="relativePath">The folder below <see cref="RootPath"/>.</param>
    /// <returns>The absolute path of the repository.</returns>
    public async Task<string> AddRepositoryAsync(string relativePath)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(path);
        await GitCli.RunAsync(path, "init", "--quiet", "--initial-branch=main").ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(path, "README.md"), "seed\n", cancellationToken).ConfigureAwait(true);
        await GitCli.RunAsync(path, "add", "README.md").ConfigureAwait(true);
        await GitCli.RunAsync(
            path,
            "-c",
            "user.name=Autonogrammer Test",
            "-c",
            "user.email=test@example.invalid",
            "-c",
            "commit.gpgsign=false",
            "commit",
            "--quiet",
            "-m",
            "seed").ConfigureAwait(true);
        return path;
    }

    /// <summary>
    /// Creates a plain folder that is not a git repository.
    /// </summary>
    /// <param name="relativePath">The folder below <see cref="RootPath"/>.</param>
    /// <returns>The absolute path of the folder.</returns>
    public string AddPlainFolder(string relativePath)
    {
        var path = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "notes.txt"), "plain\n");
        return path;
    }

    /// <summary>
    /// Deletes the temporary directory, clearing read-only attributes first so git object files do not block deletion.
    /// </summary>
    public void Dispose()
    {
        if (!Directory.Exists(RootPath))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(RootPath, recursive: true);
    }
}
