using System.Reflection;
using System.Text;

namespace Autonogrammer.Cowork.Chat;

/// <summary>
/// Writes <c>cowork-system-prompt.md</c> from the embedded base prompt. A missing or unwritable file refuses the start:
/// omp would otherwise read the path as literal text. There is no Code prompt fallback.
/// </summary>
public static class CoworkSystemPrompt
{
    /// <summary>The file name under the session directory.</summary>
    public const string FileName = "cowork-system-prompt.md";

    private const string ResourceName = "Autonogrammer.Cowork.Chat.cowork-system-prompt.md";
    private const UnixFileMode OwnerDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Reads the embedded base prompt.</summary>
    /// <returns>The base text.</returns>
    /// <exception cref="InvalidOperationException">The embedded resource is missing or over the base budget.</exception>
    public static string ReadBase()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The Cowork system prompt resource is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = reader.ReadToEnd();
        CoworkSystemPromptOptions.RequireWithin(text, CoworkSystemPromptOptions.BaseBytes, "base");
        return text;
    }

    /// <summary>
    /// Joins the base prompt with the optional sections. A section over its budget, or a total over 8 KiB, is refused.
    /// </summary>
    /// <param name="standingContext">The standing-context paragraph, or null.</param>
    /// <param name="browserGuidance">Browser guidance, or null.</param>
    /// <param name="computerGuidance">Computer guidance, or null.</param>
    /// <param name="baseText">The base prompt, or null to read the embedded resource.</param>
    /// <returns>The text to write.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A section or the total is over budget.</exception>
    public static string Compose(string? standingContext = null, string? browserGuidance = null, string? computerGuidance = null, string? baseText = null)
    {
        CoworkSystemPromptOptions.RequireWithin(standingContext, CoworkSystemPromptOptions.StandingContextBytes, "standing-context");
        CoworkSystemPromptOptions.RequireWithin(browserGuidance, CoworkSystemPromptOptions.BrowserGuidanceBytes, "browser guidance");
        CoworkSystemPromptOptions.RequireWithin(computerGuidance, CoworkSystemPromptOptions.ComputerGuidanceBytes, "computer guidance");
        var resolvedBase = baseText ?? ReadBase();
        CoworkSystemPromptOptions.RequireWithin(resolvedBase, CoworkSystemPromptOptions.BaseBytes, "base");
        var builder = new StringBuilder(resolvedBase);
        Append(builder, standingContext);
        Append(builder, browserGuidance);
        Append(builder, computerGuidance);
        var text = builder.ToString();
        CoworkSystemPromptOptions.RequireWithin(text, CoworkSystemPromptOptions.TotalBytes, "total");
        return text;
    }

    /// <summary>
    /// Writes the prompt into the session directory, owner-only, and returns its absolute path.
    /// </summary>
    /// <param name="sessionDirectory">The session directory. It is created when missing.</param>
    /// <param name="standingContext">The standing-context paragraph, or null.</param>
    /// <param name="browserGuidance">Browser guidance, or null.</param>
    /// <param name="computerGuidance">Computer guidance, or null.</param>
    /// <returns>The absolute path of the prompt file.</returns>
    /// <exception cref="CoworkPromptUnavailableException">The file cannot be written. The start is refused.</exception>
    public static string Write(string sessionDirectory, string? standingContext = null, string? browserGuidance = null, string? computerGuidance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        string text;
        try
        {
            text = Compose(standingContext, browserGuidance, computerGuidance);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or InvalidOperationException)
        {
            throw new CoworkPromptUnavailableException(exception.Message, exception);
        }

        try
        {
            var directory = Path.GetFullPath(sessionDirectory);
            if (File.Exists(directory))
            {
                throw new IOException($"'{directory}' is a file, so the Cowork prompt cannot be written.");
            }

            EnsureDirectory(directory);
            var path = Path.Combine(directory, FileName);
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
                stream.Write(Utf8.GetBytes(text));
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, OwnerFileMode);
            }

            return path;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new CoworkPromptUnavailableException(exception.Message, exception);
        }
    }

    private static void Append(StringBuilder builder, string? section)
    {
        if (string.IsNullOrEmpty(section))
        {
            return;
        }

        if (builder.Length > 0 && builder[^1] != '\n')
        {
            builder.Append('\n');
        }

        builder.Append(section);
    }

    private static void EnsureDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(directory);
            return;
        }

        _ = Directory.CreateDirectory(directory, OwnerDirectoryMode);
    }
}
