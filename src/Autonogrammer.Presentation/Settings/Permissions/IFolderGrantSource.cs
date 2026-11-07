#pragma warning disable MA0048 // The plan owns this file for the model and its related types.
namespace Autonogrammer.Presentation.Settings.Permissions;

/// <summary>Whether a granted folder may be written.</summary>
public enum FolderAccessMode
{
    /// <summary>Reads are allowed. Writes are refused.</summary>
    ReadOnly = 0,

    /// <summary>Reads and writes are allowed.</summary>
    ReadWrite = 1,
}

/// <summary>Where a folder grant is stored.</summary>
public enum FolderGrantScope
{
    /// <summary>The current session only.</summary>
    Session = 0,

    /// <summary>The project. A new session of the same project keeps the grant.</summary>
    Project = 1,

    /// <summary>The account.</summary>
    Global = 2,
}

/// <summary>One folder grant the Permissions page shows.</summary>
/// <param name="Path">The folder.</param>
/// <param name="Access">The access mode.</param>
/// <param name="Scope">The scope.</param>
public sealed record FolderGrantDraft(string Path, FolderAccessMode Access, FolderGrantScope Scope);

/// <summary>Lists, grants and revokes folder access. The native adapter is written by a later plan.</summary>
public interface IFolderGrantSource
{
    /// <summary>Returns the grants.</summary>
    /// <returns>The grants.</returns>
    IReadOnlyList<FolderGrantDraft> List();

    /// <summary>Saves a grant.</summary>
    /// <param name="grant">The grant.</param>
    void Grant(FolderGrantDraft grant);

    /// <summary>Removes a grant.</summary>
    /// <param name="path">The folder.</param>
    void Revoke(string path);
}
