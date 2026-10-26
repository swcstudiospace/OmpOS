using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Settings;

/// <summary>What <see cref="HeadSettings"/> needs beyond the head's engines: the seams a test replaces and what the production head owns for the Models page.</summary>
internal sealed class HeadSettingsOptions
{
    /// <summary>Gets how the Models page is built, or null for a Settings window without one.</summary>
    public Func<ISettingsPage>? ModelsPageFactory { get; init; }

    /// <summary>Gets what the head disposes with the settings, such as the Models page's composition, or null for nothing.</summary>
    public IDisposable? Owned { get; init; }

    /// <summary>Gets the lookup of the environment variables the Tab page reads, or null to read the process environment.</summary>
    public Func<string, string?>? GetVariable { get; init; }

    /// <summary>Gets the slash commands the Commands page lists, or null for the shipped catalogue.</summary>
    public SlashCommandRegistry? Commands { get; init; }

    /// <summary>Gets how often the sweeper applies the Runtime settings to the sessions, or null to run none. The production head sets <see cref="SessionLifecycleSweeper.DefaultInterval"/>.</summary>
    public TimeSpan? SweepInterval { get; init; }

    /// <summary>Gets how a Settings window is built over its model, or null for the real window. A test supplies a fake.</summary>
    public Func<SettingsWindowModel, ISettingsWindowHandle>? WindowFactory { get; init; }
}
