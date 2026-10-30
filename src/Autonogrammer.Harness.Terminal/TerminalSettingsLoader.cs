using System.Globalization;
using System.Text.Json;

namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// Loads <see cref="TerminalSettings"/> from a JSON settings file. Auto-run stays off unless the file contains
/// <c>"autoRun": true</c>; an absent, empty, malformed or otherwise unusable file yields the defaults.
/// </summary>
/// <remarks>
/// <para>File shape: <c>{ "autoRun": true, "maxOutputCharacters": 65536 }</c>. Both members are optional.</para>
/// <para>
/// The loader makes no decision of its own: nothing here turns auto-run on except the literal JSON <c>true</c> for
/// <c>autoRun</c>. A text value such as <c>"true"</c>, a number, an unknown member or a syntax error makes the whole
/// file unusable, so a typo can never switch commands to run without an approval.
/// </para>
/// </remarks>
public static class TerminalSettingsLoader
{
    /// <summary>
    /// Loads the settings from a file. A file that does not exist is not a problem: the defaults are used.
    /// </summary>
    /// <param name="path">The settings file path.</param>
    /// <returns>The settings and, when the file was unreadable or invalid, the reason.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty or white space.</exception>
    public static TerminalSettingsLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string json;
        try
        {
            if (!File.Exists(path))
            {
                return new TerminalSettingsLoadResult(new TerminalSettings(), Problem: null);
            }

            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unusable($"The terminal settings file '{path}' could not be read: {exception.Message}");
        }

        var result = Parse(json);
        return result.HasProblem
            ? result with { Problem = $"The terminal settings file '{path}' is unusable: {result.Problem}" }
            : result;
    }

    /// <summary>
    /// Parses settings text. Blank text is the defaults.
    /// </summary>
    /// <param name="json">The settings file text.</param>
    /// <returns>The settings and, when the text was invalid, the reason.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    public static TerminalSettingsLoadResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new TerminalSettingsLoadResult(new TerminalSettings(), Problem: null);
        }

        TerminalSettingsFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, TerminalSettingsJsonContext.Default.TerminalSettingsFile);
        }
        catch (JsonException exception)
        {
            return Unusable(exception.Message);
        }

        if (file is null)
        {
            return Unusable("the file must contain a JSON object.");
        }

        if (file.MaxOutputCharacters is < 1)
        {
            return Unusable(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"maxOutputCharacters must be at least 1 but was {file.MaxOutputCharacters}."));
        }

        var defaults = new TerminalSettings();
        var settings = new TerminalSettings
        {
            AutoRunEnabled = file.AutoRun is true,
            MaxOutputCharacters = file.MaxOutputCharacters ?? defaults.MaxOutputCharacters,
        };
        return new TerminalSettingsLoadResult(settings, Problem: null);
    }

    private static TerminalSettingsLoadResult Unusable(string problem) =>
        new(new TerminalSettings(), problem);
}
