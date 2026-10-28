namespace Autonogrammer.Workspace.Ignore;

/// <summary>
/// The settings of an <see cref="IgnoreMatcher"/>: which ignore files it reads, how big they may be, and how paths are compared. Every cap makes an over-limit input
/// skipped and reported in <see cref="IgnoreMatcher.Diagnostics"/>; nothing here makes the matcher throw.
/// </summary>
public sealed record IgnoreOptions
{
    /// <summary>Gets the platform default for <see cref="CaseSensitive"/>: <see langword="true"/> everywhere except Windows and macOS, whose usual file systems ignore case.</summary>
    public static bool PlatformCaseSensitive { get; } = !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS();

    /// <summary>Gets whether patterns and directory names are compared with case. The default is <see cref="PlatformCaseSensitive"/>.</summary>
    public bool CaseSensitive { get; init; } = PlatformCaseSensitive;

    /// <summary>Gets the most bytes an ignore file may hold. A larger file is skipped and reported. The default is 256 KiB.</summary>
    public int MaxFileBytes { get; init; } = 256 * 1024;

    /// <summary>Gets the most rules one file (or the global list) may hold. Rules past the limit are skipped and reported once. The default is 10 000.</summary>
    public int MaxRulesPerFile { get; init; } = 10_000;

    /// <summary>Gets the deepest path, in segments, that is evaluated. A deeper path counts as ignored, like an unsafe path. The default is 64.</summary>
    public int MaxPathDepth { get; init; } = 64;

    /// <summary>Gets the longest rule, in characters, that is compiled. A longer line is skipped and reported. The default is 1024.</summary>
    public int MaxPatternLength { get; init; } = 1024;

    /// <summary>
    /// Gets the most matching steps one rule may spend on one path. A rule that runs out of steps counts as matching when it excludes and as not matching when it
    /// re-includes, so an adversarial pattern can only hide a file, never expose one. The default is 1 000 000.
    /// </summary>
    public int MaxMatchWork { get; init; } = 1_000_000;

    /// <summary>Gets the patterns of the user's global ignore list, in gitignore syntax. They rank below every ignore file and above the built-in list. Empty by default.</summary>
    public IReadOnlyList<string> GlobalPatterns { get; init; } = [];

    /// <summary>Gets whether the built-in list (<see cref="IgnoreDefaults.Patterns"/>) applies. The default is <see langword="true"/>.</summary>
    public bool UseDefaultList { get; init; } = true;

    /// <summary>Gets whether <c>.gitignore</c> files are read as well. In one directory they rank below the files in <see cref="IgnoreFileNames"/>. The default is <see langword="true"/>.</summary>
    public bool RespectGitIgnore { get; init; } = true;

    /// <summary>
    /// Gets the ignore file names to read in each directory, relative to that directory and lowest rank first. A name may hold a folder, as in <c>.autonogrammer/ignore</c>;
    /// its rules apply to the directory that holds the folder. The default is <c>.cursorignore</c> and <c>.autonogrammer/ignore</c>.
    /// </summary>
    public IReadOnlyList<string> IgnoreFileNames { get; init; } = [".cursorignore", ".autonogrammer/ignore"];
}
