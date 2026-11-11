using Autonogrammer.Harness.Isolation;

namespace Autonogrammer.Harness.Isolation.Tests;

/// <summary>
/// A uniquely owned temporary fixture: a home with <c>.ssh</c> and <c>.omp</c>, an existing grant and scratch
/// directory, one existing directory, one existing file and one path that never exists. Disposal removes the tree.
/// </summary>
public sealed class TempIsolationFixture : IDisposable
{
    /// <summary>Gets the fixture root directory.</summary>
    public string Root { get; }

    /// <summary>Gets the home directory with <c>.ssh</c> and <c>.omp</c> present.</summary>
    public string Home { get; }

    /// <summary>Gets an existing read-write grant directory.</summary>
    public string GrantPath { get; }

    /// <summary>Gets an existing scratch directory.</summary>
    public string ScratchPath { get; }

    /// <summary>Gets an existing directory usable as a tmpfs deny target.</summary>
    public string ExistingDirectory { get; }

    /// <summary>Gets an existing file usable as a file deny target.</summary>
    public string ExistingFile { get; }

    /// <summary>Gets a path that never exists on the host.</summary>
    public string AbsentPath { get; }

    /// <summary>Creates the fixture tree under the system temporary directory.</summary>
    public TempIsolationFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "autonogrammer-isolation-tests-" + Guid.NewGuid().ToString("N"));
        Home = Path.Combine(Root, "home");
        GrantPath = Path.Combine(Root, "grant");
        ScratchPath = Path.Combine(Root, "scratch");
        ExistingDirectory = Path.Combine(Root, "existing-dir");
        ExistingFile = Path.Combine(Root, "existing-file");
        AbsentPath = Path.Combine(Root, "absent");
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Path.Combine(Home, ".ssh"));
        Directory.CreateDirectory(Path.Combine(Home, ".omp"));
        Directory.CreateDirectory(GrantPath);
        Directory.CreateDirectory(ScratchPath);
        Directory.CreateDirectory(ExistingDirectory);
        File.WriteAllText(ExistingFile, string.Empty);
    }

    /// <summary>Builds a Cowork policy over the fixture: home, scratch and a read-write grant; the optional second
    /// path is a read-only grant.</summary>
    /// <param name="readOnlyGrantPath">An additional read-only grant path, or null for none.</param>
    /// <returns>The policy.</returns>
    public IsolationPolicy CoworkPolicy(string? readOnlyGrantPath = null)
    {
        var grants = new List<IsolationGrant> { new(GrantPath, true) };
        if (readOnlyGrantPath is not null)
        {
            grants.Add(new IsolationGrant(readOnlyGrantPath, false));
        }

        return IsolationPolicy.ForCowork(Home, ScratchPath, grants);
    }

    /// <summary>Removes the fixture tree.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
