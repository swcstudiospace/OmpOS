using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Presentation.Hooks;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>
/// The Hooks page over the API: a read-only list of the effective hooks of the server workspace. Enabling, moving and consenting are refused with a plain
/// sentence, because a web head never changes or runs a hook. Consent is the hook's pre-consent only: the API does not know the consent a desktop recorded.
/// </summary>
internal sealed class WorkspaceHooksSource : IHooksSource
{
    /// <summary>The sentence a refused change shows.</summary>
    internal const string ReadOnlyMessage = "Hooks are read-only in the web app. Change them in the workspace files or the desktop app.";

    private readonly WorkspaceSettingsClient _client;
    private IReadOnlyList<HookEntry> _snapshot = [];

    /// <summary>Initializes a new instance of the <see cref="WorkspaceHooksSource"/> class.</summary>
    /// <param name="client">The settings client.</param>
    internal WorkspaceHooksSource(WorkspaceSettingsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc/>
    /// <exception cref="HooksSourceException">The hooks cannot be read; the message says why.</exception>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        HooksResponse response;
        try
        {
            response = await _client.GetHooksAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (WorkspaceSettingsFailure.IsExpected(exception))
        {
            throw new HooksSourceException(WorkspaceSettingsFailure.Describe(exception), exception);
        }

        if (!response.Available)
        {
            throw new HooksSourceException(response.Reason ?? "The server has no workspace folder to read hooks from.");
        }

        _snapshot = [.. response.Hooks.Select(ToEntry)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<HookEntry> Snapshot() => _snapshot;

    /// <inheritdoc/>
    public Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken) =>
        Task.FromException(new HooksSourceException(ReadOnlyMessage));

    /// <inheritdoc/>
    public Task MoveAsync(string hookId, int index, CancellationToken cancellationToken) =>
        Task.FromException(new HooksSourceException(ReadOnlyMessage));

    /// <inheritdoc/>
    public Task GrantConsentAsync(string hookId, CancellationToken cancellationToken) =>
        Task.FromException(new HooksSourceException(ReadOnlyMessage));

    private static HookEntry ToEntry(WorkspaceHookInfo hook)
    {
        IReadOnlyList<string> arguments = [];
        if (hook.Args is { Count: > 0 } args)
        {
            arguments = args;
        }
        else if (hook.Command is { } command)
        {
            arguments = [command];
        }

        return new HookEntry(hook.Id, hook.PluginId, hook.EventName, arguments, hook.WorkingDirectory, hook.Enabled, hook.PreConsented, null);
    }
}
