namespace OmpOS.Workspace.Conventions;

/// <summary>One entry of a folder listing returned by an <see cref="IConventionFileSource"/>.</summary>
/// <param name="Name">The entry name, without its folder.</param>
/// <param name="IsDirectory">Whether the entry is a folder (a symbolic link to a folder counts as a folder).</param>
/// <param name="IsSymbolicLink">Whether the entry is a symbolic link. Discovery never walks into a linked folder it was not told to scan.</param>
public sealed record ConventionEntry(string Name, bool IsDirectory, bool IsSymbolicLink = false);
