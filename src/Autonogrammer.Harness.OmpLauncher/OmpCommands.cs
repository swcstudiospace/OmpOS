namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>The fixed names and arguments the managed install uses. Shared by the installer and the resolver.</summary>
internal static class OmpCommands
{
    /// <summary>The package entry point that <c>bun build --compile</c> turns into the executable.</summary>
    public const string EntryPoint = "dist/cli.js";

    /// <summary>The file written into a staged build once its health check has passed.</summary>
    public const string VerifiedMarker = ".omp-verified";

    /// <summary>The arguments that install the package's dependencies for production.</summary>
    public static IReadOnlyList<string> InstallArguments { get; } = ["install", "--production"];

    /// <summary>The arguments that ask omp for its version.</summary>
    public static IReadOnlyList<string> VersionArguments { get; } = ["--version"];
}
