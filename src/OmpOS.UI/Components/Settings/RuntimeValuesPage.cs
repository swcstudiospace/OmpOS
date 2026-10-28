using System.Globalization;
using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Presentation.Settings;

namespace OmpOS.UI.Components.Settings;

/// <summary>
/// The Runtime page of the web head: the values the service runs with (harness adapter, stores, run lease, request caps, rate limits and the
/// workspace switches), read-only. The Presentation Runtime page edits two desktop values (idle stop and retention) and does not apply to a
/// browser; this page shows what a browser can see of the service instead.
/// </summary>
public sealed class RuntimeValuesPage : SettingsPageBase
{
    private readonly IWorkspaceSettingsReader _reader;
    private IReadOnlyList<SettingsValueGroup> _groups = [];

    /// <summary>Initializes a new instance of the <see cref="RuntimeValuesPage"/> class.</summary>
    /// <param name="reader">Where the values are read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    public RuntimeValuesPage(IWorkspaceSettingsReader reader)
        : base(SettingsSection.Runtime, SettingsCopy.RuntimeTitle, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    /// <summary>Gets the groups of values, empty until the page has loaded.</summary>
    public IReadOnlyList<SettingsValueGroup> Groups => _groups;

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(
        async token =>
        {
            var values = await _reader.GetRuntimeAsync(token).ConfigureAwait(true);
            _groups = Describe(values);
            NotifyPropertyChanged(nameof(Groups));
        },
        cancellationToken);

    private static IReadOnlyList<SettingsValueGroup> Describe(RuntimeSettingsResponse values) =>
    [
        new SettingsValueGroup(
            "Engine and stores",
            [
                new SettingsValueRow("Harness adapter", values.HarnessAdapter, "The engine adapter the service starts sessions with."),
                new SettingsValueRow("Session store", values.SessionStore, "Where sessions and their events are kept."),
                new SettingsValueRow("Coordination store", values.CoordinationStore, "Where run leases and claims are kept."),
            ]),
        new SettingsValueGroup(
            "Run lease",
            [
                new SettingsValueRow("Lease time", Seconds(values.RunLeaseSeconds), "How long a run holds its session before another worker may take it over."),
                new SettingsValueRow("Renew every", Seconds(values.RunLeaseRenewSeconds), "How often a running turn renews its lease."),
            ]),
        new SettingsValueGroup(
            "Request limits",
            [
                new SettingsValueRow("Longest prompt", Count(values.MaxPromptLength) + " characters"),
                new SettingsValueRow("Largest request body", Count(values.MaxRequestBodyBytes) + " bytes"),
                new SettingsValueRow("Prompts per minute", Count(values.PromptsPerMinute), "Shared by every caller."),
                new SettingsValueRow("New sessions per minute", Count(values.SessionsPerMinute), "Shared by every caller."),
                new SettingsValueRow("Workspace reads per minute", Count(values.WorkspaceRequestsPerMinute), "The budget of these settings pages and the workspace file views."),
                new SettingsValueRow("Largest workspace file", Count(values.MaxWorkspaceFileBytes) + " bytes"),
            ]),
        new SettingsValueGroup(
            "Workspace",
            [
                new SettingsValueRow("Workspace folder", values.WorkspaceConfigured ? "Configured" : "Not configured", "The folder the service reads rules, skills, commands and hooks from."),
                new SettingsValueRow("User scope", values.IncludesUserScope ? "Included" : "Not included", "Whether the service also reads the home folder of its own account."),
            ]),
    ];

    private static string Seconds(double value) => value.ToString("0.###", CultureInfo.InvariantCulture) + " s";

    private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
