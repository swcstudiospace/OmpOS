namespace OmpOS.Harness.Remote;

/// <summary>One concrete host alias found in an ssh configuration file, for the host picker.</summary>
/// <param name="Alias">The alias, a <c>Host</c> name without wildcards.</param>
/// <param name="SourceFile">The configuration file that declares it (the main file or an included one).</param>
/// <param name="Line">The 1-based line of the <c>Host</c> directive in <paramref name="SourceFile"/>.</param>
public sealed record SshHostEntry(string Alias, string SourceFile, int Line);
