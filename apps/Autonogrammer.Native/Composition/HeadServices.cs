using System.Collections;
using Autonogrammer.Accounts;
using Autonogrammer.Accounts.Desktop;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.AgentLoop.Safety;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Remote;
using Autonogrammer.Harness.Remote.Containers;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Harness.Secrets.Os;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Cloud;
using Autonogrammer.Native.Composition.Automations;
using Autonogrammer.Native.Composition.Checkpoints;
using Autonogrammer.Native.Composition.Connectors;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.Safety;
using Autonogrammer.Presentation.Accounts;
using Autonogrammer.Presentation.Checkpoints;
using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Commands.Handlers;
using Autonogrammer.Presentation.Landing;
using Autonogrammer.Presentation.Modes;
using Autonogrammer.Presentation.Remote;
using Autonogrammer.Presentation.Safety;
using Autonogrammer.Presentation.Worktrees;
using Autonogrammer.Workspace;
using Autonogrammer.Workspace.Checkpoints;
using Autonogrammer.Workspace.Sets;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.Logging;
using IAboutLauncher = Autonogrammer.Native.Views.IAboutLauncher;
using IUrlLauncher = Autonogrammer.Native.Views.IUrlLauncher;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// The services of the whole head that more than one window or session uses, constructed once when the application starts and disposed when it ends: the hooks service, the remote environment
/// profiles and host, the MCP server catalog, the checkpoint and worktree stores, the About window, the display settings, the run mode, the workspace sets and, when an API address is configured,
/// the cloud agents host. The shell and the Settings window read the members; the engine set reads the same instances for every session it composes.
/// </summary>
/// <remarks>
/// Every store lives in the layout the other stores of the head already use: the files of the user in the per-user <c>Autonogrammer</c> configuration folder, the state of sessions, hooks, checkpoints and worktrees
/// below the state root. Nothing here starts a process or opens a connection except the cloud agents host's own requests when its page asks.
/// </remarks>
internal sealed class HeadServices : IAsyncDisposable
{
    /// <summary>The variable that holds the address of the Autonogrammer API. Unset means the head has no cloud agents.</summary>
    internal const string ApiBaseUrlVariable = "AUTONOGRAMMER_API_BASE_URL";

    /// <summary>The variable that holds the bearer token for the Autonogrammer API. Only its name is ever shown; the value is read for each request.</summary>
    internal const string ApiTokenVariable = "AUTONOGRAMMER_API_TOKEN";

    private static readonly Action<ILogger, string, Exception?> HooksReleaseFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "HooksReleaseFailed"), "Releasing the hooks service failed ({ExceptionType}).");

    private static readonly Action<ILogger, string, Exception?> UserDataMigrationWarning =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "UserDataMigrationWarning"), "{Warning}");

    private readonly HttpClient? _apiClient;
    private readonly FileSetupConsentStore _consentStore;
    private readonly IDisposable? _cloudDisposable;
    private readonly DesktopAccountHost _account;
    private readonly Func<CancellationToken, ValueTask<string?>> _readToken;
    private readonly ArtifactsComposition _artifacts;
    private readonly LocalOmpStatusProbe _localOmp;
    private readonly LocalOmpInstaller? _localOmpInstaller;
    private readonly OmpManagedInstall? _ompManaged;
    private readonly ILogger? _logger;
    private int _disposed;

    private HeadServices(
        HeadServicesOptions options,
        string homeDirectory,
        string configDirectory,
        HooksService hooks,
        ForwardingHookApprovalPrompt hookApproval,
        EnvironmentProfileStore remoteProfiles,
        IRemoteEnvironmentsHost remoteEnvironments,
        McpServerCatalog mcp,
        CheckpointStore checkpoints,
        ForwardingSetupConsentPrompt setupConsent,
        FileSetupConsentStore consentStore,
        IWorktreeBackend worktrees,
        IAboutLauncher about,
        IUrlLauncher urlLauncher,
        DisplaySettings display,
        RunModeHub runModeHub,
        IRunModeSource runModes,
        WorkspaceSetStore workspaceSets,
        ICloudAgentsHost? cloud,
        HttpClient? apiClient,
        SshClientOptions sshOptions,
        ContainerHosts containers,
        DesktopAccountHost account,
        Func<CancellationToken, ValueTask<string?>> readToken,
        LocalOmpStatusProbe localOmp,
        OmpManagedInstall? ompManaged)
    {
        SshOptions = sshOptions;
        PodmanOptions = containers.Podman;
        WslOptions = containers.Wsl;
        ContainerEnvironments = containers.Environments;
        ContainerLister = containers.Lister;
        _artifacts = new ArtifactsComposition(new NativeCoworkAccountGate(account.Session), options.StateRoot, apiClient, readToken, account.Session);
        StateRoot = options.StateRoot;
        PluginsRoot = options.PluginsRoot;
        WorkspaceRoot = options.WorkspaceRoot;
        HomeDirectory = homeDirectory;
        ConfigDirectory = configDirectory;
        TimeProvider = options.TimeProvider ?? TimeProvider.System;
        LoggerFactory = options.LoggerFactory;
        Hooks = hooks;
        HookApproval = hookApproval;
        RemoteProfiles = remoteProfiles;
        RemoteEnvironments = remoteEnvironments;
        Mcp = mcp;
        Checkpoints = checkpoints;
        SetupConsent = setupConsent;
        _consentStore = consentStore;
        Worktrees = worktrees;
        About = about;
        UrlLauncher = urlLauncher;
        Display = display;
        RunModeHub = runModeHub;
        RunModes = runModes;
        WorkspaceSets = workspaceSets;
        Cloud = cloud;
        CheckpointBackend = new CheckpointBackend(checkpoints);
        _cloudDisposable = cloud as IDisposable;
        _apiClient = apiClient;
        _logger = options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Composition");
        Account = account;
        _account = account;
        _readToken = readToken;
        _localOmp = localOmp;
        (_localOmpInstaller, RemoteOmp) = CreateOmpWiring(ompManaged, remoteEnvironments, sshOptions, TimeProvider);
        _ompManaged = ompManaged;
        // Created last: nothing after it can throw, so the automations never outlive a failed constructor.
        Automations = CreateAutomations(options, configDirectory, cloud, TimeProvider);
    }

    /// <summary>Builds the local omp installer and the remote omp provisioner of a managed install, or nulls for both when there is none.</summary>
    private static (LocalOmpInstaller? Installer, IRemoteOmpProvisioner? Remote) CreateOmpWiring(OmpManagedInstall? ompManaged, IRemoteEnvironmentsHost remoteEnvironments, SshClientOptions sshOptions, TimeProvider clock) =>
        ompManaged is null
            ? (null, null)
            : (new LocalOmpInstaller(ompManaged), RemoteOmpProvisioner.ForManagedInstall(remoteEnvironments, ompManaged.Options, sshOptions, clock));

    /// <summary>Gets the root of all session state.</summary>
    public string StateRoot { get; }

    /// <summary>Gets the plugins root, or null when there is none.</summary>
    public string? PluginsRoot { get; }

    /// <summary>Gets the absolute workspace root of the window.</summary>
    public string WorkspaceRoot { get; }

    /// <summary>Gets the user's home directory.</summary>
    public string HomeDirectory { get; }

    /// <summary>Gets the directory of the per-user Autonogrammer files.</summary>
    public string ConfigDirectory { get; }

    /// <summary>Gets the clock of the head.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>Gets the factory of the services' loggers, or null.</summary>
    public ILoggerFactory? LoggerFactory { get; }

    /// <summary>Gets the hooks service: the hook files of every workspace, the consent and quarantine store, and the hooks of each session.</summary>
    public HooksService Hooks { get; }

    /// <summary>Gets the forwarder of the question a hook asks the user. The shell connects the approval card to <see cref="ForwardingHookApprovalPrompt.Target"/>.</summary>
    public ForwardingHookApprovalPrompt HookApproval { get; }

    /// <summary>Gets the saved ssh environment profiles.</summary>
    public EnvironmentProfileStore RemoteProfiles { get; }

    /// <summary>Gets the remote environments page's host: profiles, the ssh config, the connection test.</summary>
    public IRemoteEnvironmentsHost RemoteEnvironments { get; }

    /// <summary>Gets how ssh is run for the remote environments: the control socket directory shared by the connection test and the sessions.</summary>
    public SshClientOptions SshOptions { get; }

    /// <summary>Gets how podman is run for the container environments: the executable and the time one command may take. The connection test, the listing of running containers and the sessions share it.</summary>
    public PodmanClientOptions PodmanOptions { get; }

    /// <summary>Gets how wsl.exe is run for the WSL environments.</summary>
    public WslClientOptions WslOptions { get; }

    /// <summary>Gets the host of the Containers and WSL section of the Remote page: the connection tests and the start of a stopped container. WSL is supported on Windows only.</summary>
    public IContainerEnvironmentsHost ContainerEnvironments { get; }

    /// <summary>Gets the lister of the running containers the Remote page offers to add.</summary>
    public IContainerLister ContainerLister { get; }

    /// <summary>Gets the automations of this computer: their file, their scheduler and the manager of the Automations page. The head starts the scheduler once it is open.</summary>
    public AutomationsHost Automations { get; }

    /// <summary>Gets or sets the function that opens a terminal tab running a command, which the remote host uses to let the user confirm a host key by hand. Null when the head has no terminal: the page then tells the user to run ssh once.</summary>
    public Func<RemoteTerminalCommand, CancellationToken, Task<bool>>? TrustTerminalOpener { get; set; }

    /// <summary>Gets the MCP server catalog: the servers of the user's own files, their state and the consent for each.</summary>
    public McpServerCatalog Mcp { get; }

    /// <summary>Gets the checkpoint store of the window's workspace.</summary>
    public CheckpointStore Checkpoints { get; }

    /// <summary>Gets the checkpoint backend the checkpoint models read and record through, over <see cref="Checkpoints"/>.</summary>
    public ICheckpointBackend CheckpointBackend { get; }

    /// <summary>Gets the forwarder of the question that asks the user to approve the setup commands of a worktree. The shell connects its dialog to <see cref="ForwardingSetupConsentPrompt.Target"/>.</summary>
    public ForwardingSetupConsentPrompt SetupConsent { get; }

    /// <summary>Gets the worktree backend of the window's workspace.</summary>
    public IWorktreeBackend Worktrees { get; }

    /// <summary>Gets the About window launcher (<see cref="Autonogrammer.Native.Views.IAboutLauncher"/>: it takes the owner window).</summary>
    public IAboutLauncher About { get; }

    /// <summary>Gets the launcher of links in the system browser (<see cref="Autonogrammer.Native.Views.IUrlLauncher"/>).</summary>
    public IUrlLauncher UrlLauncher { get; }

    /// <summary>Gets the display settings (vim mode, line numbers, thinking blocks, status indicators).</summary>
    public DisplaySettings Display { get; }

    /// <summary>Gets the run mode hub: the audit log and the policy holders of the open sessions.</summary>
    public RunModeHub RunModeHub { get; }

    /// <summary>Gets the source of the Run mode page. A saved mode refreshes the policy of every open session.</summary>
    public IRunModeSource RunModes { get; }

    /// <summary>Gets the store of the recent workspace sets.</summary>
    public WorkspaceSetStore WorkspaceSets { get; }

    /// <summary>Gets the cloud agents host, or null when no API address is configured.</summary>
    public ICloudAgentsHost? Cloud { get; }

    /// <summary>Gets the Omp checks and installs of the saved ssh environments, or null when the managed omp is not configured: the Remote page then hides its Omp buttons.</summary>
    public IRemoteOmpProvisioner? RemoteOmp { get; }

    /// <summary>Reads the status of omp on this computer for the Code landing: its source and version, or why it is missing.</summary>
    public Func<CancellationToken, Task<LocalOmpStatus>> ReadLocalOmp => _localOmp.ReadAsync;

    /// <summary>Checks the managed omp for a newer release after the window is shown. Null when the managed install is not consulted.</summary>
    public Func<CancellationToken, Task<LocalOmpUpdate>>? CheckLocalOmpUpdate => _localOmpInstaller is null ? null : ct => _localOmpInstaller.CheckAsync(ct);

    /// <summary>Installs the latest managed omp, reporting each stage through the callback. Null when the managed install is not consulted.</summary>
    public Func<Action<LocalOmpStatus>, CancellationToken, Task<LocalOmpUpdate>>? InstallLocalOmp =>
        _localOmpInstaller is null ? null : (progress, ct) => _localOmpInstaller.InstallAsync(progress, ct);

    /// <summary>Resolves the omp a session launch runs: the configured path when it runs, then the managed install, then PATH. Null when none is runnable.</summary>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The executable, or null.</returns>
    internal Task<string?> ResolveOmpForLaunchAsync(CancellationToken cancellationToken) => _localOmp.ResolveExecutableAsync(cancellationToken);

    /// <summary>Gets the account panel host. Null only when construction was skipped; the head always builds one.</summary>
    public IAccountHost? Account { get; }

    /// <summary>Gets the desktop account host, including the secret-store selector a test reads.</summary>
    internal DesktopAccountHost AccountHost => _account;

    /// <summary>Gets the artifact composition of the signed-in account: its store, its tool provider and its panel. The head builds it once and disposes it with the head.</summary>
    internal ArtifactsComposition Artifacts => _artifacts;

    /// <summary>Returns the bearer the cloud host sends: the signed-in account token, or the environment token only when signed out.</summary>
    /// <param name="cancellationToken">Cancels the account token read.</param>
    /// <returns>The token, or null when neither is available.</returns>
    public ValueTask<string?> ReadApiTokenAsync(CancellationToken cancellationToken) => _readToken(cancellationToken);

    /// <summary>
    /// Gets or sets the host that runs one <c>/best-of-n</c> candidate: it opens an agent session in the candidate's worktree, sends the task and returns the last assistant text. The engine set sets it
    /// once it exists; null makes the command unavailable with a reason.
    /// </summary>
    public IBestOfNHost? BestOfN { get; set; }

    /// <summary>Gets or sets the usage history MainView.CreateExport reads. The activity composition sets it.</summary>
    public Autonogrammer.Presentation.Usage.IUsageHistorySource? UsageHistory { get; set; }

    /// <summary>
    /// Gets the environment and the roots the next new session starts on. The shell sets it from the environment picker and the workspace dialog right before it asks the panel for a new session;
    /// the engine set takes it when it creates that session. One instance is shared by the shell and the engine set.
    /// </summary>
    public PendingSessionTarget NewSessionTarget { get; } = new();

    /// <summary>
    /// Creates every service. Nothing here starts a process. A file that is missing is created on its first write; the hooks service and the display settings read their files now, and a file that cannot be read
    /// leaves the defaults (the display settings) or an empty hook state rather than failing the head.
    /// </summary>
    /// <param name="options">Where the state lives and the seams a test replaces.</param>
    /// <param name="cancellationToken">Cancels the opening of the hooks service and the display settings.</param>
    /// <returns>The services. The head disposes them after every session.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static async Task<HeadServices> CreateAsync(HeadServicesOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var clock = options.TimeProvider ?? TimeProvider.System;
        var home = string.IsNullOrWhiteSpace(options.HomeDirectory) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : options.HomeDirectory;
        var config = ResolveConfigDirectory(options);
        var hookApproval = new ForwardingHookApprovalPrompt();
        var hooks = await OpenHooksAsync(options, home, hookApproval, clock, cancellationToken).ConfigureAwait(false);
        // The stores and the API client are owned here until the services take them over; a failure part way disposes the ones that exist.
        Stores? stores = null;
        HttpClient? apiClient = null;
        OmpManagedInstall? ompManaged = null;
        var created = false;
        try
        {
            stores = new Stores(options, config, clock);
            var holder = new TrustTerminalHolder();
            var sshOptions = new SshClientOptions { ControlPathDirectory = PrivateSshControlDirectory() };
            var remoteEnvironments = new RemoteEnvironmentsHost(stores.RemoteProfiles, sshOptions, openTrustTerminal: holder.OpenAsync, clock: clock);
            var mcp = new McpServerCatalog(new LocalMcpFileStore(), new FileMcpStateStore(Path.Combine(config, "mcp-state.json")), new RecordingConnectionChecker(new McpConnectionChecker()), extra: options.McpExtra);
            var setupConsent = new ForwardingSetupConsentPrompt();
            var worktrees = CreateWorktrees(options, stores.ConsentStore, setupConsent);
            var urlLauncher = new SystemUrlLauncher(options.TopLevel ?? (static () => null));
            var display = new DisplaySettings(new FileDisplaySettingsStore(Path.Combine(options.StateRoot, "config", "display.json")));
            await display.LoadAsync(cancellationToken).ConfigureAwait(false);
            var (runModeHub, runModes) = CreateRunModes(options, string.IsNullOrWhiteSpace(home) ? config : home, hooks, clock);
            var opened = await OpenAccountAsync(options, clock, urlLauncher, cancellationToken).ConfigureAwait(false);
            apiClient = opened.Client;
            var containers = CreateContainerHosts(options, clock);
            ompManaged = options.OmpManagedRoot is { } ompRoot ? new OmpManagedInstall(ompRoot, clock) : null;
            var localOmp = new LocalOmpStatusProbe(options.OmpPath, ompManaged?.Options);

            var services = new HeadServices(
                options, runModeHub.HomeDirectory, config, hooks, hookApproval, stores.RemoteProfiles, remoteEnvironments, mcp, stores.Checkpoints, setupConsent, stores.ConsentStore,
                worktrees, new AboutLauncher(), urlLauncher, display, runModeHub, runModes, stores.WorkspaceSets, opened.Cloud, apiClient, sshOptions, containers, opened.Account, opened.Token, localOmp, ompManaged);
            holder.Owner = services;
            created = true;

            // The services own the stores, the client and the omp probe from here on.
            stores = null;
            apiClient = null;
            ompManaged = null;
            return services;
        }
        finally
        {
            apiClient?.Dispose();
            ompManaged?.Dispose();
            stores?.Dispose();
            if (!created)
            {
                await hooks.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static WorktreeBackend CreateWorktrees(HeadServicesOptions options, FileSetupConsentStore consentStore, ForwardingSetupConsentPrompt setupConsent) =>
        new(
            Path.Combine(options.StateRoot, "worktrees"),
            () => options.WorkspaceRoot,
            new ConsentedWorktreeSetupRunner(new WorktreeSetupConsentAdapter(consentStore, setupConsent), new ShellWorktreeSetupExecutor()));

    private static (RunModeHub Hub, RefreshingRunModeSource Source) CreateRunModes(HeadServicesOptions options, string modeHome, HooksService hooks, TimeProvider clock)
    {
        var hub = new RunModeHub(modeHome, new FileRunModeStore(modeHome), hooks.IsWorkspaceTrusted, clock);
        var source = new RefreshingRunModeSource(
            new RunModeSettingsSource(hub.HomeDirectory, () => options.WorkspaceRoot, hub.Store, () => hooks.IsWorkspaceTrusted(options.WorkspaceRoot)),
            hub);
        return (hub, source);
    }

    private static async Task<(ICloudAgentsHost? Cloud, HttpClient? Client, DesktopAccountHost Account, Func<CancellationToken, ValueTask<string?>> Token)> OpenAccountAsync(
        HeadServicesOptions options,
        TimeProvider clock,
        IUrlLauncher urlLauncher,
        CancellationToken cancellationToken)
    {
        var variable = options.GetVariable ?? (static name => Environment.GetEnvironmentVariable(name));
        var address = ApiAddress(variable);
        var client = address is null ? null : CreateApiClient(options.ApiHandler);
        DesktopAccountHost? account = null;
        try
        {
#pragma warning disable CA2000 // The host is returned to HeadServices, which disposes it; DisposeAsync is the release path.
            account = CreateAccount(options, clock, client, address);
#pragma warning restore CA2000
            DesktopAccountHost owned = account;
            var readToken = new Func<CancellationToken, ValueTask<string?>>(token => ResolveTokenAsync(owned.Session, variable, token));
            var cloud = CreateCloudHost(options, clock, urlLauncher, client, address, readToken);
            account = null;
            return (cloud, client, owned, readToken);
        }
        finally
        {
            if (account is not null)
            {
                await account.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Creates the cloud agents host when an API address is configured. The token supplier prefers the signed-in account token.</summary>
    private static ApiCloudAgentsHost? CreateCloudHost(
        HeadServicesOptions options,
        TimeProvider clock,
        IUrlLauncher urlLauncher,
        HttpClient? client,
        Uri? address,
        Func<CancellationToken, ValueTask<string?>> readToken)
    {
        if (address is null || client is null)
        {
            return null;
        }

        return new ApiCloudAgentsHost(
            client,
            address,
            clock,
            readToken,
            urlLauncher: urlLauncher,
            logger: options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Cloud"));
    }

    private static async ValueTask<string?> ResolveTokenAsync(IAccountSession session, Func<string, string?> variable, CancellationToken cancellationToken)
    {
        if (session.State.Phase == AuthPhase.SignedIn)
        {
            AuthResult<SecretString> token = await session.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            return token.IsSuccess ? token.Value.Reveal() : null;
        }

        return Token(variable);
    }

    private static DesktopAccountHost CreateAccount(HeadServicesOptions options, TimeProvider clock, HttpClient? client, Uri? address)
    {
        var settings = new FileSettingsStore(options.StateRoot);
        var clientLogger = options.LoggerFactory?.CreateLogger<GoTrueClient>();
        if (options.AccountSession is { } injected)
        {
            return new DesktopAccountHost(injected, null, null, null, null, settings, client, address, clock, clientLogger);
        }

        ISecretStore os = options.AccountOsStore ?? new OsSecretStore(options.LoggerFactory?.CreateLogger<OsSecretStore>(), clock);
        ISecretStore file = options.AccountFileStore ?? new OwnerOnlyFileSecretStore(Path.Combine(settings.ConfigDirectory, "account-secrets.json"));
        var selector = new AccountSecretStoreSelector(
            os,
            file,
            settings.ReadHasStoredSession(),
            settings.ReadAllowFileSecretStore(),
            options.LoggerFactory?.CreateLogger<AccountSecretStoreSelector>());
        var guard = new CrossProcessFileLock(
            Path.Combine(settings.ConfigDirectory, "account-refresh.lock"),
            clock,
            options.LoggerFactory?.CreateLogger<CrossProcessFileLock>());
        DesktopAccountHost? host = null;
        Func<CancellationToken, ValueTask<GoTrueClient?>> provider = token => host!.ProvideClientAsync(token);
        var manager = new AccountSessionManager(selector, provider, clock, guard, options.LoggerFactory?.CreateLogger<AccountSessionManager>());
        var coordinator = new SignInCoordinator(manager, provider, clock, options.LoggerFactory?.CreateLogger<SignInCoordinator>());
        var flow = new PkceSignInFlow(coordinator, clock, options.LoggerFactory?.CreateLogger<PkceSignInFlow>());
        host = new DesktopAccountHost(manager, manager, coordinator, flow, selector, settings, client, address, clock, clientLogger);
        return host;
    }

    private static ContainerHosts CreateContainerHosts(HeadServicesOptions options, TimeProvider clock)
    {
        var podman = options.Podman ?? new PodmanClientOptions();
        var wsl = options.Wsl ?? new WslClientOptions();
        return new ContainerHosts(podman, wsl, new ContainerEnvironmentsHost(podman, wsl, clock), new PodmanContainerLister(podman, clock));
    }

    private static AutomationsHost CreateAutomations(HeadServicesOptions options, string config, ICloudAgentsHost? cloud, TimeProvider clock)
    {
        var localAvailable = options.OmpAvailable?.Invoke() ?? OmpLocator.Locate(OmpLocatorOptions.FromEnvironment(options.OmpPath)) is OmpFound;
        return new AutomationsHost(
            Path.Combine(config, "automations.json"),
            Path.Combine(config, "automation-consent.json"),
            cloud,
            () => options.WorkspaceRoot,
            localAvailable,
            clock,
            options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Automations"));
    }

    private static Task<HooksService> OpenHooksAsync(HeadServicesOptions options, string home, ForwardingHookApprovalPrompt approval, TimeProvider clock, CancellationToken cancellationToken) =>
        HooksService.OpenAsync(
            new HooksServiceOptions
            {
                StateDirectory = Path.Combine(options.StateRoot, "hooks"),
                SessionStateRoot = options.StateRoot,
                PluginsRoot = options.PluginsRoot,
                HomeDirectory = string.IsNullOrWhiteSpace(home) ? null : home,
                ApprovalPrompt = approval,
                Clock = clock,
                LoggerFactory = options.LoggerFactory,
            },
            cancellationToken);

    private static HttpClient CreateApiClient(HttpMessageHandler? handler) =>
        handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);

    /// <summary>
    /// Returns the locator context the MCP catalog reads its files with: the workspace roots (primary first), the user's home directory and the process environment, for the expansion of <c>${VAR}</c> in the files.
    /// </summary>
    /// <param name="workspaceRoots">The workspace roots of the session, primary first.</param>
    /// <returns>The context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="workspaceRoots"/> is null.</exception>
    public McpLocatorContext McpContext(IReadOnlyList<string> workspaceRoots)
    {
        ArgumentNullException.ThrowIfNull(workspaceRoots);
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                variables[key] = entry.Value as string;
            }
        }

        return new McpLocatorContext(workspaceRoots, HomeDirectory, variables);
    }

    /// <summary>Creates the source of the skills a custom mode can pin, for the workspace roots of a session and the user's home.</summary>
    /// <param name="workspaceRoot">The workspace root of the session.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is blank.</exception>
    public IModeSkillSource CreateModeSkills(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        return new ConventionModeSkillSource([workspaceRoot], string.IsNullOrWhiteSpace(HomeDirectory) ? null : HomeDirectory);
    }

    /// <summary>Releases every service that holds a handle: the cloud client, the stores and the hooks service. The head calls it after every session is stopped.</summary>
    /// <returns>A task that completes when every service is released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // The automations stop first: a run in flight uses the cloud host and the sessions.
        await Automations.DisposeAsync().ConfigureAwait(false);
        _artifacts.Dispose();
        await _account.DisposeAsync().ConfigureAwait(false);
        _cloudDisposable?.Dispose();
        _apiClient?.Dispose();
        _ompManaged?.Dispose();
        Checkpoints.Dispose();
        _consentStore.Dispose();
        WorkspaceSets.Dispose();
        RemoteProfiles.Dispose();
        try
        {
            await Hooks.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The hooks service holds only its state files; a failure to release them must not hold the shutdown.
            if (_logger is not null)
            {
                HooksReleaseFailed(_logger, exception.GetType().Name, null);
            }
        }
    }

    /// <summary>Returns the configured configuration directory as it is, or resolves the default one (see <see cref="ResolveDefaultConfigDirectory"/>), which is the only one that is migrated.</summary>
    private static string ResolveConfigDirectory(HeadServicesOptions options) =>
        string.IsNullOrWhiteSpace(options.ConfigDirectory)
            ? ResolveDefaultConfigDirectory(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                options.StateRoot,
                options.LoggerFactory)
            : options.ConfigDirectory;

    /// <summary>
    /// Resolves the root the managed omp install owns. The installer writes only below an <c>omp</c> folder of this root, so the other folders of
    /// the root, such as the DPAPI secrets, are never touched. The root is the per-user local data directory, which a roaming profile does not copy;
    /// with no local data folder it is the state root.
    /// </summary>
    /// <param name="localApplicationData">The per-user local application data folder. Empty when the platform has none.</param>
    /// <param name="stateRoot">The state root the root falls back to.</param>
    /// <returns>The managed root.</returns>
    internal static string ResolveManagedOmpRoot(string localApplicationData, string stateRoot) =>
        string.IsNullOrEmpty(localApplicationData) ? stateRoot : Autonogrammer.Harness.Abstractions.UserDataDirectory.PathFor(localApplicationData);

    /// <summary>
    /// Resolves the directory the head keeps its configuration in when none was configured, and moves a legacy per-user data directory into it once.
    /// This is the head services' call of <see cref="Autonogrammer.Harness.Abstractions.UserDataDirectory.EnsureCurrent"/>: it applies to this default root only, never to
    /// a configured <see cref="HeadServicesOptions.ConfigDirectory"/>. A warning reaches the factory's logger, or the trace listeners when there is no factory.
    /// </summary>
    /// <param name="applicationData">The per-user application data folder. Empty when the platform has none.</param>
    /// <param name="stateRoot">The state root the config directory falls back under when there is no application data folder.</param>
    /// <param name="loggerFactory">The factory of the head's loggers, or null.</param>
    /// <returns>The configuration directory.</returns>
    internal static string ResolveDefaultConfigDirectory(string applicationData, string stateRoot, ILoggerFactory? loggerFactory)
    {
        if (string.IsNullOrEmpty(applicationData))
        {
            return Path.Combine(stateRoot, "config");
        }

        var logger = loggerFactory?.CreateLogger("Autonogrammer.Native.Composition");
        Action<string>? warn = logger is null ? null : warning => UserDataMigrationWarning(logger, warning, null);
        return Autonogrammer.Harness.Abstractions.UserDataDirectory.EnsureCurrent(applicationData, warn).Path;
    }

    /// <summary>Returns the workspace root when it exists, otherwise the state root, which the store creates: a checkpoint store refuses a root that is missing.</summary>
    private static string ExistingRoot(HeadServicesOptions options)
    {
        if (Directory.Exists(options.WorkspaceRoot))
        {
            return options.WorkspaceRoot;
        }

        Directory.CreateDirectory(options.StateRoot);
        return options.StateRoot;
    }

    private static Uri? ApiAddress(Func<string, string?> variable)
    {
        var text = variable(ApiBaseUrlVariable);
        return !string.IsNullOrWhiteSpace(text)
            && Uri.TryCreate(text.Trim(), UriKind.Absolute, out var address)
            && (string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || string.Equals(address.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
                ? address
                : null;
    }

    private static string? Token(Func<string, string?> variable)
    {
        var token = variable(ApiTokenVariable);
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    /// <summary>
    /// Returns a short directory only the current user can enter, for the ssh control sockets (a unix socket path is limited to about 100 characters, so the state root is too long), or
    /// null when none can be made safely: ssh then runs without connection sharing.
    /// </summary>
    private static string? PrivateSshControlDirectory()
    {
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "autonogrammer-ssh-" + Environment.UserName);
            SessionEngineSet.EnsurePrivateDirectory(directory);
            if (!OperatingSystem.IsWindows())
            {
                const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
                if (File.GetUnixFileMode(directory) != ownerOnly)
                {
                    File.SetUnixFileMode(directory, ownerOnly);
                }
            }

            return directory;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>How podman and wsl.exe are run, and the hosts built over them.</summary>
    private sealed record ContainerHosts(PodmanClientOptions Podman, WslClientOptions Wsl, IContainerEnvironmentsHost Environments, IContainerLister Lister);

    /// <summary>Forwards the remote host's request to open a terminal to the function the shell connects later.</summary>
    private sealed class TrustTerminalHolder
    {
        public HeadServices? Owner { get; set; }

        public Task<bool> OpenAsync(RemoteTerminalCommand command, CancellationToken cancellationToken) =>
            Owner?.TrustTerminalOpener is { } opener ? opener(command, cancellationToken) : Task.FromResult(false);
    }

    /// <summary>The stores that hold a file handle, made together so a failure part way releases the ones that exist.</summary>
    private sealed class Stores : IDisposable
    {
        public Stores(HeadServicesOptions options, string config, TimeProvider clock)
        {
            RemoteProfiles = new EnvironmentProfileStore(Path.Combine(config, "environments.json"), clock);
            Checkpoints = new CheckpointStore(options.StateRoot, [ExistingRoot(options)], clock: clock);
            ConsentStore = new FileSetupConsentStore(Path.Combine(config, "setup-consent.json"), clock);
            WorkspaceSets = new WorkspaceSetStore(Path.Combine(config, "workspace-sets.json"), clock);
        }

        public EnvironmentProfileStore RemoteProfiles { get; }

        public CheckpointStore Checkpoints { get; }

        public FileSetupConsentStore ConsentStore { get; }

        public WorkspaceSetStore WorkspaceSets { get; }

        public void Dispose()
        {
            WorkspaceSets.Dispose();
            ConsentStore.Dispose();
            Checkpoints.Dispose();
            RemoteProfiles.Dispose();
        }
    }
}
