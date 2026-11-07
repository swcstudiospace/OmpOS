using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// Projects <see cref="CoworkSessionRegistry"/> onto <see cref="ICoworkSessionCatalog"/> and builds the services the window binds.
/// The views library never sees the registry.
/// </summary>
internal sealed class CoworkSessionCatalogAdapter : ICoworkSessionCatalog
{
    private readonly CoworkSessionRegistry _registry;

    private CoworkSessionCatalogAdapter(CoworkSessionRegistry registry) => _registry = registry;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/> after a task is created or updated outside a snapshot.</summary>
    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Builds the services when an account is signed in. A signed-out gate, or a head with no services, returns null and opens no file.
    /// </summary>
    /// <param name="head">The composed head.</param>
    /// <returns>The services, or null when Cowork is not available.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="head"/> is null.</exception>
    public static CoworkSurfaceServices? TryBuild(AgentHead head)
    {
        ArgumentNullException.ThrowIfNull(head);
        if (head.Services is null)
        {
            return null;
        }

        var gate = new NativeCoworkAccountGate(head.Services.AccountHost.Session);
        if (!gate.IsSignedIn)
        {
            return null;
        }

        var registry = CoworkSessionRegistry.Open(
            new CoworkSessionOptions { StateRoot = StateRootOf(head.Settings.Store) },
            gate,
            TimeProvider.System);
        _ = registry.RecoverAfterRestart();
        var catalog = new CoworkSessionCatalogAdapter(registry);
        return new CoworkSurfaceServices
        {
            Catalog = catalog,
            Shell = new CoworkShellModel(catalog),
            SavedSurface = ReadSaved(head.Settings.Store),
            PersistSurface = surface => head.Settings.Store.SetSurface(surface),
            ReadCodeRows = () => head.Panel.Catalog.Sessions,
            SubscribeCode = static _ => { },
            UnsubscribeCode = static _ => { },
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<CoworkSessionSummary> Snapshot() =>
        [.. _registry.List().Select(static row => new CoworkSessionSummary
        {
            Id = row.Id,
            Title = row.Title,
            Status = row.Status,
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        })];

    private static string StateRootOf(FileSettingsStore store) =>
        Path.GetDirectoryName(store.ConfigDirectory) ?? store.ConfigDirectory;

    private static string? ReadSaved(FileSettingsStore store)
    {
        try
        {
            return store.Read().Surface;
        }
        catch (SettingsSourceException)
        {
            return null;
        }
    }
}
