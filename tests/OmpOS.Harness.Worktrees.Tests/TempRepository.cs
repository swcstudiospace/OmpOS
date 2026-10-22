using Xunit;

namespace OmpOS.Worktrees.Tests;

/// <summary>
/// A fresh temporary directory holding a git repository with one commit, plus an operator worktree root. Disposing it deletes everything.
/// </summary>
internal sealed class TempRepository : IDisposable
{
    private TempRepository(string rootPath)
    {
        RootPath = rootPath;
    }

    /// <summary>
    /// Gets the temporary directory that contains the repository and the worktree root.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Gets the path of the main checkout.
    /// </summary>
    public string RepositoryPath => Path.Combine(RootPath, "repo");

    /// <summary>
    /// Gets the operator-controlled directory that holds run worktrees. It does not exist until a run is created.
    /// </summary>
    public string WorktreeRoot => Path.Combine(RootPath, "worktrees");

    /// <summary>
    /// Creates a repository with one commit on <c>main</c>.
    /// </summary>
    /// <returns>The fixture.</returns>
    public static async Task<TempRepository> CreateAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var rootPath = Path.Combine(Path.GetTempPath(), "ompos-worktrees-" + Guid.NewGuid().ToString("N"));
        var repository = new TempRepository(rootPath);

        Directory.CreateDirectory(repository.RepositoryPath);
        await GitCli.RunAsync(repository.RepositoryPath, "init", "--quiet", "--initial-branch=main").ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(repository.RepositoryPath, "README.md"), "seed\n", cancellationToken).ConfigureAwait(true);
        await GitCli.RunAsync(repository.RepositoryPath, "add", "README.md").ConfigureAwait(true);
        await GitCli.RunAsync(
            repository.RepositoryPath,
            "-c",
            "user.name=OmpOS Test",
            "-c",
            "user.email=test@example.invalid",
            "-c",
            "commit.gpgsign=false",
            "commit",
            "--quiet",
            "-m",
            "seed").ConfigureAwait(true);

        return repository;
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
