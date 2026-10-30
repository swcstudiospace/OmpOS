using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Models;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>
/// A scripted <see cref="IModelsSettingsSource"/>: it returns the snapshot the test sets, answers each action with the result the test sets,
/// records every call, and applies a saved or removed custom provider to its snapshot so the page's reload shows it. No process starts.
/// </summary>
internal sealed class FakeModelsSource : IModelsSettingsSource
{
    /// <summary>Gets or sets the snapshot a load returns.</summary>
    public ModelsSnapshot Snapshot { get; set; } = ModelsData.Snapshot();

    /// <summary>Gets or sets the reason every load fails with, or null.</summary>
    public string? LoadFailure { get; set; }

    /// <summary>Gets or sets a gate that holds a load until the test completes it.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets or sets the result of saving a key.</summary>
    public ModelsActionResult KeyResult { get; set; } = ModelsActionResult.Success;

    /// <summary>Gets or sets the result of setting the default model.</summary>
    public ModelsActionResult DefaultResult { get; set; } = ModelsActionResult.Success;

    /// <summary>Gets or sets the result of saving a custom provider.</summary>
    public ModelsActionResult SaveCustomResult { get; set; } = ModelsActionResult.Success;

    /// <summary>Gets or sets the result of removing a custom provider.</summary>
    public ModelsActionResult RemoveResult { get; set; } = ModelsActionResult.Success;

    /// <summary>Gets or sets the outcome of a connection test.</summary>
    public ProviderTestOutcome TestOutcome { get; set; } = new(ProviderTestStatus.Reachable, "Answered on the endpoint.");

    /// <summary>Gets or sets a gate that holds a connection test until the test completes it.</summary>
    public TaskCompletionSource? TestGate { get; set; }

    /// <summary>Gets how many loads were started.</summary>
    public int LoadCount { get; private set; }

    /// <summary>Gets the provider id and key of every key save, in order.</summary>
    public List<(string ProviderId, string Key)> Keys { get; } = [];

    /// <summary>Gets the selector of every default change, in order.</summary>
    public List<string> Defaults { get; } = [];

    /// <summary>Gets every custom provider form that was saved, in order.</summary>
    public List<CustomProviderForm> SavedForms { get; } = [];

    /// <summary>Gets the id of every removed provider, in order.</summary>
    public List<string> Removed { get; } = [];

    /// <summary>Gets the id of every tested provider, in order.</summary>
    public List<string> Tested { get; } = [];

    /// <inheritdoc />
    public async Task<ModelsSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCount++;
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        return LoadFailure is { } failure ? throw new SettingsSourceException(failure) : Snapshot;
    }

    /// <inheritdoc />
    public Task<ModelsActionResult> SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken)
    {
        Keys.Add((providerId, apiKey));
        if (KeyResult.Succeeded)
        {
            Snapshot = Snapshot with
            {
                Providers =
                [
                    .. Snapshot.Providers.Select(provider => string.Equals(provider.Id, providerId, StringComparison.Ordinal)
                        ? provider with { Credential = CredentialState.Present }
                        : provider),
                ],
            };
        }

        return Task.FromResult(KeyResult);
    }

    /// <inheritdoc />
    public Task<ModelsActionResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken)
    {
        Defaults.Add(selector);
        if (DefaultResult.Succeeded)
        {
            Snapshot = Snapshot with { DefaultModel = selector };
        }

        return Task.FromResult(DefaultResult);
    }

    /// <inheritdoc />
    public Task<ModelsActionResult> SaveCustomProviderAsync(CustomProviderForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        SavedForms.Add(form);
        if (SaveCustomResult.Succeeded)
        {
            var providers = Snapshot.Providers.Where(provider => !string.Equals(provider.Id, form.Id, StringComparison.Ordinal)).ToList();
            providers.Add(ModelsData.Saved(form));
            Snapshot = Snapshot with { Providers = providers };
        }

        return Task.FromResult(SaveCustomResult);
    }

    /// <inheritdoc />
    public Task<ModelsActionResult> RemoveCustomProviderAsync(string providerId, CancellationToken cancellationToken)
    {
        Removed.Add(providerId);
        if (RemoveResult.Succeeded)
        {
            Snapshot = Snapshot with { Providers = [.. Snapshot.Providers.Where(provider => !string.Equals(provider.Id, providerId, StringComparison.Ordinal))] };
        }

        return Task.FromResult(RemoveResult);
    }

    /// <inheritdoc />
    public async Task<ProviderTestOutcome> TestAsync(string providerId, CancellationToken cancellationToken)
    {
        Tested.Add(providerId);
        if (TestGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        return TestOutcome;
    }
}
