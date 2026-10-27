using System.Globalization;
using System.Text;

namespace OmpOS.Harness.AgentLoop.Launch;

/// <summary>
/// Writes the per-session omp settings overlay that <c>omp acp --config &lt;path&gt;</c> reads, and deletes it when disposed.
/// The overlay exists only when the settings ask for an overlay key, and it holds settings, never secrets. On Unix the
/// file has mode 0600 and a directory created for it has mode 0700.
/// </summary>
public sealed class ConfigOverlayWriter : IDisposable
{
    private const UnixFileMode OwnerDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private ConfigOverlayWriter(string path) => Path = path;

    /// <summary>Gets the path of the overlay file. It equals <see cref="OmpLaunchPlan.ConfigOverlayPath"/> of the plan for the same session.</summary>
    public string Path { get; }

    /// <summary>
    /// Writes the overlay for one session when the settings ask for any overlay key.
    /// </summary>
    /// <param name="settings">What the user asked of the agent loop.</param>
    /// <param name="sessionStateDirectory">The directory returned by <see cref="SessionStateLayout.SessionDirectory"/>. It is created when missing.</param>
    /// <param name="additionalDirectories">The other roots of a multi-root workspace (see <see cref="OmpLaunchPlan.AdditionalDirectories"/>), or null.</param>
    /// <returns>The writer that owns the file, or null when no overlay key is set and nothing was written.</returns>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static ConfigOverlayWriter? Write(AgentLoopSettings settings, string sessionStateDirectory, IReadOnlyList<string>? additionalDirectories = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionStateDirectory);
        var content = Render(settings, additionalDirectories);
        if (content is null)
        {
            return null;
        }

        var path = SessionStateLayout.ConfigOverlayFile(sessionStateDirectory);
        EnsureDirectory(System.IO.Path.GetDirectoryName(path) ?? sessionStateDirectory);
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerFileMode;
        }

        using (var stream = new FileStream(path, options))
        {
            stream.Write(Utf8WithoutBom.GetBytes(content));
        }

        if (!OperatingSystem.IsWindows())
        {
            // A file that already existed keeps its old mode when it is truncated, so set the mode explicitly.
            File.SetUnixFileMode(path, OwnerFileMode);
        }

        return new ConfigOverlayWriter(path);
    }

    /// <summary>
    /// Deletes the overlay file. A file that is already gone is not an error, and calling the method again does nothing.
    /// </summary>
    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The overlay holds settings only, so a file that cannot be removed leaks nothing; disposal must not throw.
        }
    }

    /// <summary>
    /// Renders the overlay as a YAML mapping, or returns null when the settings ask for no overlay key. The additional
    /// directories of a multi-root session are recorded under <c>workspace.additionalDirectories</c> only when an overlay is
    /// written for another reason: the <c>--add-dir</c> arguments of the plan are what carries them, and omp merges both
    /// without duplicates.
    /// </summary>
    /// <param name="settings">What the user asked of the agent loop.</param>
    /// <param name="additionalDirectories">The other roots of a multi-root workspace, or null for none.</param>
    internal static string? Render(AgentLoopSettings settings, IReadOnlyList<string>? additionalDirectories = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.DisableProjectMcpConfig)
        {
            return null;
        }

        var builder = new StringBuilder("mcp:\n  enableProjectConfig: false\n");
        if (additionalDirectories is { Count: > 0 })
        {
            builder.Append("workspace:\n  additionalDirectories:\n");
            foreach (var directory in additionalDirectories)
            {
                builder.Append("    - ");
                AppendQuoted(builder, directory);
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static void AppendQuoted(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case < ' ':
                    builder.Append(CultureInfo.InvariantCulture, $"\\x{(int)character:x2}");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        builder.Append('"');
    }

    private static void EnsureDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            return;
        }

        Directory.CreateDirectory(directory, OwnerDirectoryMode);
    }
}
