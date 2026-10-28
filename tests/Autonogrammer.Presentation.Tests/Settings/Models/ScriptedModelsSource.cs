using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Models;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>A scripted <see cref="IModelsSettingsSource"/> that behaves like a small omp: writes change what the next load returns.</summary>
internal sealed class ScriptedModelsSource : IModelsSettingsSource
{
    private readonly List<ProviderInfo> _providers;

    /// <summary>Creates a source that lists <paramref name="providers"/>.</summary>
    public ScriptedModelsSource(params ProviderInfo[] providers)
    {
        _providers = [.. providers];
    }

    /// <summary>Gets or sets the default model the source reports.</summary>
    public string? DefaultModel { get; set; }

    /// <summary>Gets or sets the warning the source reports.</summary>
    public string? Warning { get; set; }

    /// <summary>Gets or sets whether the source reports a complete list.</summary>
    public bool IsComplete { get; set; } = true;

    /// <summary>Gets the provider templates the source reports.</summary>
    public List<ProviderTemplate> Templates { get; } = [];

    /// <summary>Gets or sets the message a load fails with; null lets loads succeed.</summary>
    public string? LoadFailureMessage { get; set; }

    /// <summary>Gets or sets a gate a load waits for.</summary>
    public TaskCompletionSource? LoadGate { get; set; }

    /// <summary>Gets or sets the message a load after the first fails with; null lets them succeed.</summary>
    public string? ReloadFailureMessage { get; set; }

    /// <summary>Gets the number of loads.</summary>
    public int LoadCalls { get; private set; }

    /// <summary>Gets the keys handed over, with the provider each was for.</summary>
    public List<(string ProviderId, string Key)> KeysReceived { get; } = [];

    /// <summary>Gets or sets the result of a key write.</summary>
    public ModelsActionResult KeyResult { get; set; } = ModelsActionResult.Success;

    /// <summary>Gets or sets whether a failed key write quotes the key in its message (a faulty source).</summary>
    public bool EchoKeyInFailure { get; set; }

    /// <summary>Gets or sets whether a key write throws the expected source exception.</summary>
    public bool ThrowOnKey { get; set; }

    /// <summary>Gets or sets a gate a key write waits for.</summary>
    public TaskCompletionSource? KeyGate { get; set; }

    /// <summary>Gets the default models requested.</summary>
    public List<string> DefaultsRequested { get; } = [];

    /// <summary>Gets or sets the result of a default write.</summary>
    public ModelsActionResult DefaultResult { get; set; } = ModelsActionResult.Success;

    /// <summary>Gets the custom provider forms saved.</summary>
    public List<CustomProviderForm> CustomSaved { get; } = [];

    /// <summary>Gets or sets the result of a custom provider write.</summary>
    public ModelsActionResult CustomResult { get; set; } = ModelsActionResult.Success;

    /// <summary>Gets the provider ids removed.</summary>
    public List<string> Removed { get; } = [];

    /// <summary>Gets or sets the result of a removal.</summary>
    public ModelsActionResult RemoveResult { get; set; } = ModelsActionResult.Success;

    /// <summary>Gets the provider ids tested.</summary>
    public List<string> Tested { get; } = [];

    /// <summary>Gets the scripted test outcomes by provider id; a provider without one is not testable with no message.</summary>
    public Dictionary<string, ProviderTestOutcome> TestOutcomes { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the message a test throws the expected source exception with; null lets tests answer.</summary>
    public string? TestThrowsMessage { get; set; }

    /// <summary>Gets or sets a gate a test waits for.</summary>
    public TaskCompletionSource? TestGate { get; set; }

    /// <inheritdoc/>
    public async Task<ModelsSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCalls++;
        if (LoadGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (LoadFailureMessage is not null || (LoadCalls > 1 && ReloadFailureMessage is not null))
        {
            throw new SettingsSourceException(LoadFailureMessage ?? ReloadFailureMessage ?? string.Empty);
        }

        return new ModelsSnapshot([.. _providers], DefaultModel, Warning, IsComplete, [.. Templates]);
    }

    /// <inheritdoc/>
    public async Task<ModelsActionResult> SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken)
    {
        KeysReceived.Add((providerId, apiKey));
        if (KeyGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        if (ThrowOnKey)
        {
            throw new SettingsSourceException($"omp rejected {apiKey}");
        }

        if (!KeyResult.Succeeded)
        {
            return EchoKeyInFailure ? ModelsActionResult.Failure($"omp refused the key {apiKey}.") : KeyResult;
        }

        var index = _providers.FindIndex(provider => string.Equals(provider.Id, providerId, StringComparison.Ordinal));
        _providers[index] = _providers[index] with { Credential = CredentialState.Present };
        return KeyResult;
    }

    /// <inheritdoc/>
    public Task<ModelsActionResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken)
    {
        DefaultsRequested.Add(selector);
        if (DefaultResult.Succeeded)
        {
            DefaultModel = selector;
        }

        return Task.FromResult(DefaultResult);
    }

    /// <inheritdoc/>
    public Task<ModelsActionResult> SaveCustomProviderAsync(CustomProviderForm form, CancellationToken cancellationToken)
    {
        CustomSaved.Add(form);
        if (!CustomResult.Succeeded)
        {
            return Task.FromResult(CustomResult);
        }

        var info = new ProviderInfo(
            form.Id,
            form.DisplayName,
            ProviderGroup.Custom,
            form.BaseEndpoint.OriginalString,
            form.Auth == CustomProviderAuth.None ? ProviderAuthKind.None : ProviderAuthKind.ApiKey,
            form.Auth == CustomProviderAuth.None ? CredentialState.NotRequired : CredentialState.Absent,
            [.. form.Models.Select(model => $"{form.Id}/{model}")],
            null,
            null,
            null,
            true,
            null,
            form);
        var index = _providers.FindIndex(provider => string.Equals(provider.Id, form.Id, StringComparison.Ordinal));
        if (index >= 0)
        {
            _providers[index] = info;
        }
        else
        {
            _providers.Add(info);
        }

        return Task.FromResult(CustomResult);
    }

    /// <inheritdoc/>
    public Task<ModelsActionResult> RemoveCustomProviderAsync(string providerId, CancellationToken cancellationToken)
    {
        Removed.Add(providerId);
        if (RemoveResult.Succeeded)
        {
            _providers.RemoveAll(provider => string.Equals(provider.Id, providerId, StringComparison.Ordinal));
        }

        return Task.FromResult(RemoveResult);
    }

    /// <inheritdoc/>
    public async Task<ProviderTestOutcome> TestAsync(string providerId, CancellationToken cancellationToken)
    {
        Tested.Add(providerId);
        if (TestGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        if (TestThrowsMessage is { } message)
        {
            throw new SettingsSourceException(message);
        }

        return TestOutcomes.TryGetValue(providerId, out var outcome) ? outcome : new ProviderTestOutcome(ProviderTestStatus.NotTestable, string.Empty);
    }
}
