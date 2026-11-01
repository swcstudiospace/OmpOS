#pragma warning disable MA0048 // The plan owns this file for the snapshot, the source and the model.
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork.Memory;

/// <summary>Account memory settings. Defaults stay off.</summary>
/// <param name="Enabled">Whether memory is enabled.</param>
/// <param name="Paused">Whether memory is paused.</param>
/// <param name="IncludeSensitive">Whether sensitive categories may be saved.</param>
public sealed record MemorySettingsSnapshot(bool Enabled, bool Paused, bool IncludeSensitive);

/// <summary>Loads and saves memory settings.</summary>
public interface IMemorySettingsSource
{
    /// <summary>Loads settings.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The settings.</returns>
    Task<CoworkChannelResult<MemorySettingsSnapshot>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Saves settings.</summary>
    /// <param name="snapshot">The settings.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    Task<CoworkChannelResult<bool>> SaveAsync(MemorySettingsSnapshot snapshot, CancellationToken cancellationToken);
}

/// <summary>
/// Enabled, paused and include_sensitive. The page states the English-only poisoning limitation and the residual risk.
/// </summary>
public sealed class MemorySettingsModel : ObservableModel
{
    /// <summary>The detectors are English-only. This limitation is stated on the page.</summary>
    public const string PoisoningLimitationNotice = "Memory refusal checks are English-only. Other languages are not detected.";

    /// <summary>The residual risk accepted on the Memory page.</summary>
    public const string ResidualRiskSentence = "A page the agent read can make it save text that steers later sessions. Every entry names its source session and can be deleted.";

    private readonly IMemorySettingsSource _source;
    private bool _enabled;
    private bool _paused;
    private bool _includeSensitive;
    private CoworkViewState _state = CoworkViewState.Loading;
    private string? _sentence;

    /// <summary>Creates the settings model.</summary>
    /// <param name="source">The settings source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public MemorySettingsModel(IMemorySettingsSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets whether memory is enabled.</summary>
    public bool Enabled => _enabled;

    /// <summary>Gets whether memory is paused.</summary>
    public bool Paused => _paused;

    /// <summary>Gets whether sensitive categories may be saved.</summary>
    public bool IncludeSensitive => _includeSensitive;

    /// <summary>Gets the view state.</summary>
    public CoworkViewState State => _state;

    /// <summary>Gets the degraded sentence, or null.</summary>
    public string? Sentence => _sentence;

    /// <summary>Loads settings.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the state is set.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        SetState(CoworkViewState.Loading, "Loading memory settings.");
        var result = await _source.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (result.Offline)
        {
            SetState(CoworkViewState.Offline, CoworkDegradedSentences.Offline);
            return;
        }

        if (!result.Succeeded || result.Value is null)
        {
            SetState(CoworkViewState.Error, Named(result.State) ?? "Memory settings could not be loaded.");
            return;
        }

        Apply(result.Value);
        SetState(CoworkViewState.Ready, result.Value.Paused ? CoworkDegradedSentences.MemoryPaused : null);
    }

    /// <summary>Saves the three flags.</summary>
    /// <param name="enabled">Whether memory is enabled.</param>
    /// <param name="paused">Whether memory is paused.</param>
    /// <param name="includeSensitive">Whether sensitive categories may be saved.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the save was accepted.</returns>
    public async Task<bool> SaveAsync(bool enabled, bool paused, bool includeSensitive, CancellationToken cancellationToken)
    {
        var snapshot = new MemorySettingsSnapshot(enabled, paused, includeSensitive);
        var result = await _source.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
        if (result.Offline)
        {
            SetState(CoworkViewState.Offline, CoworkDegradedSentences.Offline);
            return false;
        }

        if (!result.Succeeded)
        {
            SetState(CoworkViewState.Error, Named(result.State) ?? "Memory settings could not be saved.");
            return false;
        }

        Apply(snapshot);
        SetState(CoworkViewState.Ready, paused ? CoworkDegradedSentences.MemoryPaused : null);
        return true;
    }

    private static string? Named(string? state) =>
        CoworkDegradedSentences.TryFor(state);

    private void Apply(MemorySettingsSnapshot snapshot)
    {
        _enabled = snapshot.Enabled;
        _paused = snapshot.Paused;
        _includeSensitive = snapshot.IncludeSensitive;
        NotifyPropertyChanged(nameof(Enabled));
        NotifyPropertyChanged(nameof(Paused));
        NotifyPropertyChanged(nameof(IncludeSensitive));
    }

    private void SetState(CoworkViewState state, string? sentence)
    {
        _state = state;
        _sentence = sentence;
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(Sentence));
    }
}
