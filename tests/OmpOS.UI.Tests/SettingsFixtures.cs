using System.ComponentModel;
using OmpOS.Presentation;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Customize;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.UI.Tests;

/// <summary>
/// Real Settings page models over scripted sources, so the web components render the same models the desktop head does. Only the sources
/// are fakes: they record what the pages write and can be told to fail.
/// </summary>
internal static class SettingsFixtures
{
    /// <summary>The general settings source: holds the values and records every save.</summary>
    internal sealed class GeneralSource : IGeneralSettingsSource
    {
        /// <summary>Gets or sets the values a load returns.</summary>
        internal GeneralSettingsValues Values { get; set; } = new(MotionPreference.System, ThemePreference.Dark);

        /// <summary>Gets the values saved, in order.</summary>
        internal List<GeneralSettingsValues> Saved { get; } = [];

        /// <inheritdoc />
        public Task<GeneralSettingsValues> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Values);

        /// <inheritdoc />
        public Task SaveAsync(GeneralSettingsValues values, CancellationToken cancellationToken)
        {
            Saved.Add(values);
            Values = values;
            return Task.CompletedTask;
        }
    }

    /// <summary>The agents settings source: holds the values and records every save.</summary>
    internal sealed class AgentsSource : IAgentsSettingsSource
    {
        /// <summary>Gets the values saved, in order.</summary>
        internal List<AgentsSettingsValues> Saved { get; } = [];

        /// <inheritdoc />
        public Task<AgentsSettingsValues> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(new AgentsSettingsValues(TerminalAutoRun: false));

        /// <inheritdoc />
        public Task SaveAsync(AgentsSettingsValues values, CancellationToken cancellationToken)
        {
            Saved.Add(values);
            return Task.CompletedTask;
        }
    }

    /// <summary>The Tab source: inline completion is off.</summary>
    internal sealed class TabSource : ITabSettingsSource
    {
        /// <inheritdoc />
        public Task<TabSettingsValues> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(new TabSettingsValues(Enabled: false, EndpointHost: null, Source: "config"));
    }

    /// <summary>The plugins source: returns the rows the test sets, or fails once when told to.</summary>
    internal sealed class PluginsSource : IPluginsSettingsSource
    {
        /// <summary>Gets or sets the rows a load returns.</summary>
        internal IReadOnlyList<PluginRow> Rows { get; set; } = [];

        /// <summary>Gets or sets a reason that makes the next load fail, or null.</summary>
        internal string? FailWith { get; set; }

        /// <inheritdoc />
        public Task<PluginsSettingsValues> LoadAsync(CancellationToken cancellationToken)
        {
            if (FailWith is { } reason)
            {
                FailWith = null;
                throw new SettingsSourceException(reason);
            }

            return Task.FromResult(new PluginsSettingsValues(Rows));
        }
    }

    /// <summary>The keyboard source: returns the rows the test sets.</summary>
    internal sealed class KeyboardSource : IKeyboardSettingsSource
    {
        /// <summary>Gets or sets the rows a load returns.</summary>
        internal IReadOnlyList<ShortcutRow> Rows { get; set; } = [];

        /// <inheritdoc />
        public Task<KeyboardShortcutsSnapshot> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(new KeyboardShortcutsSnapshot(Rows));
    }

    /// <summary>The network diagnostics source: returns the checks the test sets and counts the runs.</summary>
    internal sealed class NetworkSource : INetworkDiagnosticsSource
    {
        /// <summary>Gets or sets the checks a run returns.</summary>
        internal IReadOnlyList<EndpointCheck> Checks { get; set; } = [];

        /// <summary>Gets the number of runs.</summary>
        internal int Runs { get; private set; }

        /// <inheritdoc />
        public Task<NetworkDiagnosticsReport> RunAsync(CancellationToken cancellationToken)
        {
            Runs++;
            return Task.FromResult(new NetworkDiagnosticsReport(Checks, new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero)));
        }
    }

    /// <summary>The rules source: returns the catalogue the test sets, or fails once when told to.</summary>
    internal sealed class RulesSource : IRulesSettingsSource
    {
        /// <summary>Gets or sets the catalogue a load returns.</summary>
        internal CustomizeCatalog Catalog { get; set; } = new([], []);

        /// <summary>Gets or sets a reason that makes the next load fail, or null.</summary>
        internal string? FailWith { get; set; }

        /// <inheritdoc />
        public Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken)
        {
            if (FailWith is { } reason)
            {
                FailWith = null;
                throw new SettingsSourceException(reason);
            }

            return Task.FromResult(Catalog);
        }
    }

    /// <summary>The models source: holds the providers, records every key it receives and answers with the result the test chose.</summary>
    internal sealed class ModelsSource : IModelsSettingsSource
    {
        /// <summary>Gets the providers a load returns.</summary>
        internal List<ProviderInfo> Providers { get; } = [];

        /// <summary>Gets the listings of the provider directory.</summary>
        internal List<ProviderListing> Listings { get; } = [];

        /// <summary>Gets the keys received, as provider and key: this is the only place a key may appear.</summary>
        internal List<(string ProviderId, string Key)> Keys { get; } = [];

        /// <summary>Gets or sets the failure message of the next key save, or null to accept it. The message may quote the key, to prove the page hides it.</summary>
        internal string? KeyFailure { get; set; }

        /// <summary>Gets the catalogue requests received, as request and key.</summary>
        internal List<(CatalogueProviderRequest Request, string Key)> Catalogue { get; } = [];

        /// <summary>Gets the custom provider forms received.</summary>
        internal List<CustomProviderForm> Forms { get; } = [];

        /// <summary>Gets or sets a reason that makes the next load fail, or null.</summary>
        internal string? FailWith { get; set; }

        /// <inheritdoc />
        public Task<ModelsSnapshot> LoadAsync(CancellationToken cancellationToken)
        {
            if (FailWith is { } reason)
            {
                FailWith = null;
                throw new SettingsSourceException(reason);
            }

            return Task.FromResult(new ModelsSnapshot([.. Providers], null, null, true, [], [.. Listings]));
        }

        /// <inheritdoc />
        public Task<ModelsActionResult> SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken)
        {
            Keys.Add((providerId, apiKey));
            if (KeyFailure is { } failure)
            {
                return Task.FromResult(ModelsActionResult.Failure(failure.Replace("{key}", apiKey, StringComparison.Ordinal)));
            }

            var index = Providers.FindIndex(provider => string.Equals(provider.Id, providerId, StringComparison.Ordinal));
            if (index >= 0)
            {
                Providers[index] = Providers[index] with { Credential = CredentialState.Present };
            }

            return Task.FromResult(ModelsActionResult.Success);
        }

        /// <inheritdoc />
        public Task<ModelsActionResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken) => Task.FromResult(ModelsActionResult.Success);

        /// <inheritdoc />
        public Task<ModelsActionResult> SaveCustomProviderAsync(CustomProviderForm form, CancellationToken cancellationToken)
        {
            Forms.Add(form);
            return Task.FromResult(ModelsActionResult.Success);
        }

        /// <inheritdoc />
        public Task<ModelsActionResult> RemoveCustomProviderAsync(string providerId, CancellationToken cancellationToken) => Task.FromResult(ModelsActionResult.Success);

        /// <inheritdoc />
        public Task<ProviderTestOutcome> TestAsync(string providerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ProviderTestOutcome(ProviderTestStatus.Reachable, "reachable"));

        /// <inheritdoc />
        public Task<ModelsActionResult> AddCatalogueProviderAsync(CatalogueProviderRequest request, string apiKey, CancellationToken cancellationToken)
        {
            Catalogue.Add((request, apiKey));
            return Task.FromResult(ModelsActionResult.Success);
        }
    }

    /// <summary>A page the web head has no view for: it stands in for any section added later.</summary>
    internal sealed class UnknownPage : ISettingsPage
    {
        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public SettingsSection Section => SettingsSection.Commands;

        /// <inheritdoc />
        public string Title => "Commands";

        /// <inheritdoc />
        public bool IsDirty => false;

        /// <inheritdoc />
        public bool HasError => false;

        /// <inheritdoc />
        public Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task<bool> SaveAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        /// <inheritdoc />
        public void Reset()
        {
        }

        /// <inheritdoc />
        public void Discard()
        {
        }
    }

    /// <summary>Builds a cloud provider that takes an API key and has none yet.</summary>
    /// <param name="id">The provider id.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The provider.</returns>
    internal static ProviderInfo CloudProvider(string id, string name) => new(
        id,
        name,
        ProviderGroup.Cloud,
        "https://api." + id + ".test/v1",
        ProviderAuthKind.ApiKey,
        CredentialState.Absent,
        [id + "/model-1", id + "/model-2"],
        null,
        null,
        null,
        false,
        null);

    /// <summary>Builds a coding plan listing with a placeholder in its endpoint, a rule, tiers and a route that takes a key.</summary>
    /// <param name="id">The listing id.</param>
    /// <param name="name">The display name.</param>
    /// <param name="region">The region.</param>
    /// <returns>The listing.</returns>
    internal static ProviderListing PlanListing(string id, string name, ProviderListingRegion region) => new(
        id,
        name,
        region,
        null,
        null,
        ProviderListingKind.CodingPlan,
        null,
        null,
        null,
        [new ProviderListingRoute("openai", "OpenAI protocol", "https://{region}.plan.test/v1", "Bearer key", true, true, false, [])],
        [new ProviderListingVariable("region", "Region", "eu", "The region of your plan.")],
        new Uri("https://docs.plan.test/"),
        new Uri("https://keys.plan.test/"),
        new Uri("https://plan.test/pricing"),
        ProviderListingStatus.Unverified,
        new DateOnly(2026, 10, 1),
        null,
        new ProviderListingPlan(
            "Use is limited to interactive coding.",
            new Uri("https://plan.test/terms"),
            InteractiveOnly: true,
            SeparateKey: true,
            new Uri("https://plan.test/plans"),
            [new ProviderListingTier("Pro", "20 USD per month", true, "500 requests per day")],
            []));

    /// <summary>Builds a window over the Runtime, General and Agents pages, the pages a dialog test edits.</summary>
    /// <param name="general">Receives the general source.</param>
    /// <param name="agents">Receives the agents source.</param>
    /// <param name="runtimeSaved">Receives the values the Runtime page reports saved.</param>
    /// <returns>The window; its pages are not loaded yet.</returns>
    internal static SettingsWindowModel Window(out GeneralSource general, out AgentsSource agents, List<RuntimeSettingsValues> runtimeSaved)
    {
        general = new GeneralSource();
        agents = new AgentsSource();
        var runtime = new RuntimePage(new RuntimeSettingsModel());
        runtime.Saved += (_, args) => runtimeSaved.Add(args.Values);
        return new SettingsWindowModel([runtime, new GeneralPage(general), new AgentsPage(agents)]);
    }
}
