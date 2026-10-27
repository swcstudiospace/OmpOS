namespace OmpOS.Presentation.Editor;

/// <summary>
/// One folder of a multi-root workspace as the explorer opens it. The file system is created by <paramref name="Open"/> when the
/// root is opened, so a folder that cannot be reached fails only its own row.
/// </summary>
/// <param name="Id">A stable identifier of the root, unique in the workspace.</param>
/// <param name="DisplayName">The root name shown as the first level of the tree and used as the prefix of root-qualified paths. Unique ignoring case.</param>
/// <param name="IsPrimary">True for the root that is the working directory of the run.</param>
/// <param name="Open">Creates the file system of the root, for example <see cref="LocalWorkspaceFileSystem"/> over its folder.</param>
public sealed record WorkspaceRootSource(string Id, string DisplayName, bool IsPrimary, Func<IWorkspaceFileSystem> Open);
