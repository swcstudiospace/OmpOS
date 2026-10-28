using Microsoft.JSInterop;
using OmpOS.Presentation;
using OmpOS.Presentation.Settings;

namespace OmpOS.Web.Services.Settings;

/// <summary>
/// The <see cref="IGeneralSettingsSource"/> of the web head: the theme and the motion preference live in the browser's <c>localStorage</c>
/// through <c>ompos-theme.js</c>, the same module the theme switch uses, so a change applies at once and survives a reload.
/// </summary>
internal sealed class WebGeneralSettingsSource : IGeneralSettingsSource
{
    private const string ModulePath = "./_content/OmpOS.UI/scripts/ompos-theme.js";

    private readonly IJSRuntime _js;

    /// <summary>Initializes a new instance of the <see cref="WebGeneralSettingsSource"/> class.</summary>
    /// <param name="js">The JavaScript runtime.</param>
    internal WebGeneralSettingsSource(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);
        _js = js;
    }

    /// <inheritdoc />
    public async Task<GeneralSettingsValues> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var module = await _js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath).ConfigureAwait(true);
            try
            {
                var theme = await module.InvokeAsync<string?>("getTheme", cancellationToken).ConfigureAwait(true);
                var motion = await module.InvokeAsync<string?>("getMotion", cancellationToken).ConfigureAwait(true);
                return new GeneralSettingsValues(MotionOf(motion), ThemeOf(theme));
            }
            finally
            {
                await module.DisposeAsync().ConfigureAwait(true);
            }
        }
        catch (JSException fault)
        {
            throw new SettingsSourceException("The browser did not let the page read its saved theme and motion choice.", fault);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(GeneralSettingsValues values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        try
        {
            var module = await _js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath).ConfigureAwait(true);
            try
            {
                await module.InvokeVoidAsync("setTheme", cancellationToken, ThemeKey(values.Theme)).ConfigureAwait(true);
                await module.InvokeVoidAsync("setMotion", cancellationToken, MotionKey(values.Motion)).ConfigureAwait(true);
            }
            finally
            {
                await module.DisposeAsync().ConfigureAwait(true);
            }
        }
        catch (JSException fault)
        {
            throw new SettingsSourceException("The browser did not let the page save the choice.", fault);
        }
    }

    /// <summary>Maps a stored theme key to the preference. An unknown key reads as Light, the page default.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The preference.</returns>
    internal static ThemePreference ThemeOf(string? key) => key switch
    {
        "system" => ThemePreference.System,
        "dark" => ThemePreference.Dark,
        _ => ThemePreference.Light,
    };

    /// <summary>Maps a stored motion key to the preference. An unknown key reads as System.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The preference.</returns>
    internal static MotionPreference MotionOf(string? key) => key switch
    {
        "reduce" => MotionPreference.Reduced,
        "full" => MotionPreference.Full,
        _ => MotionPreference.System,
    };

    private static string ThemeKey(ThemePreference theme) => theme switch
    {
        ThemePreference.System => "system",
        ThemePreference.Dark => "dark",
        _ => "light",
    };

    private static string MotionKey(MotionPreference motion) => motion switch
    {
        MotionPreference.Reduced => "reduce",
        MotionPreference.Full => "full",
        _ => "system",
    };
}
