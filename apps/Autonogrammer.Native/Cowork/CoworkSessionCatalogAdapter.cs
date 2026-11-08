using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Composition;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Accounts;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// Projects <see cref="CoworkSessionRegistry"/> onto <see cref="ICoworkSessionCatalog"/> and builds the services the window binds.
/// The views library never sees the registry.
/// </summary>
internal sealed class CoworkSessionCatalogAdapter : ICoworkSessionCatalog
{
    private const string SignedOutPanelId = "cowork.signed-out";

    private readonly CoworkSessionRegistry _registry;

    private CoworkSessionCatalogAdapter(CoworkSessionRegistry registry) => _registry = registry;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/> after a task is created or updated outside a snapshot.</summary>
    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Builds the services for a head that has Cowork services. Signed in, the registry opens over the account folder. Signed out, the
    /// services show the signed-out panel over an empty catalog. That path opens no registry and reads or creates no file under the Cowork state root.
    /// </summary>
    /// <param name="head">The composed head.</param>
    /// <param name="openSignIn">Opens the sign-in window over a model built on the head's account host. The sign-in button calls it.</param>
    /// <returns>The services, or null when the head has no Cowork services.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="head"/> or <paramref name="openSignIn"/> is null.</exception>
    public static CoworkSurfaceServices? TryBuild(AgentHead head, Action<AccountViewModel> openSignIn)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(openSignIn);
        if (head.Services is null)
        {
            return null;
        }

        var account = head.Services.AccountHost;
        var gate = new NativeCoworkAccountGate(account.Session);
        if (!gate.IsSignedIn)
        {
            return BuildSignedOut(head, account, openSignIn);
        }

        var registry = CoworkSessionRegistry.Open(
            new CoworkSessionOptions { StateRoot = StateRootOf(head.Settings.Store) },
            gate,
            TimeProvider.System);
        _ = registry.RecoverAfterRestart();
        var catalog = new CoworkSessionCatalogAdapter(registry);
        var shell = new CoworkShellModel(catalog);
        head.Services.Artifacts.RegisterPanel(shell);
        return Build(head, catalog, shell, ReadSaved(head.Settings.Store));
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

    private static CoworkSurfaceServices BuildSignedOut(AgentHead head, DesktopAccountHost account, Action<AccountViewModel> openSignIn)
    {
        var catalog = new SignedOutCatalog();
        var shell = new CoworkShellModel(catalog);
        shell.RegisterPanel(new SignedOutPanel(SignedOutContentOf(account, openSignIn)));
        return Build(head, catalog, shell, savedSurface: null);
    }

    /// <summary>
    /// The signed-out content. When the API has no client sign-in block the content has no button. Otherwise the button opens the sign-in window
    /// over one model on the head's account host; the model is built here, once, so each click does not add a subscription to the host.
    /// </summary>
    private static CoworkSignedOutContent SignedOutContentOf(DesktopAccountHost account, Action<AccountViewModel> openSignIn)
    {
        if (!account.State.Configured)
        {
            return new CoworkSignedOutContent(CoworkSignedOutState.NotConfigured, signIn: null);
        }

        var model = new AccountViewModel(account);
        return new CoworkSignedOutContent(CoworkSignedOutState.SignInAvailable, () => openSignIn(model));
    }

    private static CoworkSurfaceServices Build(AgentHead head, ICoworkSessionCatalog catalog, CoworkShellModel shell, string? savedSurface) => new()
    {
        Catalog = catalog,
        Shell = shell,
        SavedSurface = savedSurface,
        PersistSurface = surface => head.Settings.Store.SetSurface(surface),
        ReadCodeRows = () => head.Panel.Catalog.Sessions,
        SubscribeCode = static _ => { },
        UnsubscribeCode = static _ => { },
    };

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

    /// <summary>The catalog of a signed-out head. It reports no tasks and never raises <see cref="Changed"/>.</summary>
    private sealed class SignedOutCatalog : ICoworkSessionCatalog
    {
        /// <inheritdoc />
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public IReadOnlyList<CoworkSessionSummary> Snapshot() => [];
    }

    /// <summary>The one dock panel a signed-out head shows. Its body is the signed-out content.</summary>
    private sealed class SignedOutPanel(CoworkSignedOutContent content) : IDockPanel
    {
        /// <inheritdoc />
        public string Id => SignedOutPanelId;

        /// <inheritdoc />
        public string Title => CoworkCopy.SignInToUseCowork;

        /// <inheritdoc />
        public object Content { get; } = content;
    }
}
