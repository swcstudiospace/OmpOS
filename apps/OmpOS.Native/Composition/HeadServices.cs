using System.Collections;
using Microsoft.Extensions.Logging;
using OmpOS.Harness.AgentLoop.Hooks;
using OmpOS.Harness.AgentLoop.Safety;
using OmpOS.Harness.Mcp.Catalog;
using OmpOS.Harness.Mcp.Config;
using OmpOS.Harness.Remote;
using OmpOS.Native.Agents;
using OmpOS.Native.Cloud;
using OmpOS.Native.Composition.Checkpoints;
using OmpOS.Native.Views;
using OmpOS.Native.Views.Safety;
using OmpOS.Presentation.Checkpoints;
using OmpOS.Presentation.Cloud;
using OmpOS.Presentation.Commands.Handlers;
using OmpOS.Presentation.Modes;
using OmpOS.Presentation.Remote;
using OmpOS.Presentation.Safety;
using OmpOS.Presentation.Worktrees;
using OmpOS.Workspace;
using OmpOS.Workspace.Checkpoints;
using OmpOS.Workspace.Sets;
using OmpOS.Worktrees;
using IAboutLauncher = OmpOS.Native.Views.IAboutLauncher;
using IUrlLauncher = OmpOS.Native.Views.IUrlLauncher;

namespace OmpOS.Native.Composition;

/// <summary>
/// The services of the whole head that more than one window or session uses, constructed once when the application starts and disposed when it ends: the hooks service, the remote environment
/// profiles and host, the MCP server catalog, the checkpoint and worktree stores, the About window, the display settings, the run mode, the workspace sets and, when an API address is configured,
/// the cloud agents host. The shell and the Settings window read the members; the engine set reads the same instances for every session it composes.
/// </summary>
/// <remarks>
/// Every store lives in the layout the other stores of the head already use: the files of the user in the per-user <c>OmpOS</c> configuration folder, the state of sessions, hooks, checkpoints and worktrees
/// below the state root. Nothing here starts a process or opens a connection except the cloud agents host's own requests when its page asks.
/// </remarks>
internal sealed class HeadServices : IAsyncDisposable
{
    /// <summary>The variable that holds the address of the OmpOS API. Unset means the head has no cloud agents.</summary>
    internal const string ApiBaseUrlVariable = "OMPOS_API_BASE_URL";

    /// <summary>The variable that holds the bearer token for the OmpOS API. Only its name is ever shown; the value is read for each request.</summary>
    internal const string ApiTokenVariable = "OMPOS_API_TOKEN";

    private static readonly Action<ILogger, string, Exception?> HooksReleaseFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "HooksReleaseFailed"), "Releasing the hooks service failed ({ExceptionType}).");

    private readonly HttpClient? _apiClient;
    private readonly FileSetupConsentStore _consentStore;
    private readonly IDisposable? _cloudDisposable;
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
        SshClientOptions sshOptions)
    {
        SshOptions = sshOptions;
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
        _logger = options.LoggerFactory?.CreateLogger("OmpOS.Native.Composition");
    }

    /// <summary>Gets the root of all session state.</summary>
    public string StateRoot { get; }

    /// <summary>Gets the plugins root, or null when there is none.</summary>
    public string? PluginsRoot { get; }

    /// <summary>Gets the absolute workspace root of the window.</summary>
    public string WorkspaceRoot { get; }

    /// <summary>Gets the user's home directory.</summary>
    public string HomeDirectory { get; }

    /// <summary>Gets the directory of the per-user OmpOS files.</summary>
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

    /// <summary>Gets the About window launcher (<see cref="OmpOS.Native.Views.IAboutLauncher"/>: it takes the owner window).</summary>
    public IAboutLauncher About { get; }

    /// <summary>Gets the launcher of links in the system browser (<see cref="OmpOS.Native.Views.IUrlLauncher"/>).</summary>
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

    /// <summary>
    /// Gets or sets the host that runs one <c>/best-of-n</c> candidate: it opens an agent session in the candidate's worktree, sends the task and returns the last assistant text. The engine set sets it
    /// once it exists; null makes the command unavailable with a reason.
    /// </summary>
    public IBestOfNHost? BestOfN { get; set; }

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
        var config = string.IsNullOrWhiteSpace(options.ConfigDirectory) ? DefaultConfigDirectory(options.StateRoot) : options.ConfigDirectory;
        var hookApproval = new ForwardingHookApprovalPrompt();
        var hooks = await OpenHooksAsync(options, home, hookApproval, clock, cancellationToken).ConfigureAwait(false);
        // The stores and the API client are owned here until the services take them over; a failure part way disposes the ones that exist.
        Stores? stores = null;
        HttpClient? apiClient = null;
        var created = false;
        try
        {
            stores = new Stores(options, config, clock);
            var holder = new TrustTerminalHolder();
            var sshOptions = new SshClientOptions { ControlPathDirectory = PrivateSshControlDirectory() };
            var remoteEnvironments = new RemoteEnvironmentsHost(stores.RemoteProfiles, sshOptions, openTrustTerminal: holder.OpenAsync, clock: clock);
            var mcp = new McpServerCatalog(new LocalMcpFileStore(), new FileMcpStateStore(Path.Combine(config, "mcp-state.json")));
            var setupConsent = new ForwardingSetupConsentPrompt();
            var worktrees = CreateWorktrees(options, stores.ConsentStore, setupConsent);
            var urlLauncher = new SystemUrlLauncher(options.TopLevel ?? (static () => null));
            var display = new DisplaySettings(new FileDisplaySettingsStore(Path.Combine(options.StateRoot, "config", "display.json")));
            await display.LoadAsync(cancellationToken).ConfigureAwait(false);
            var (runModeHub, runModes) = CreateRunModes(options, string.IsNullOrWhiteSpace(home) ? config : home, hooks, clock);
            var (cloud, client) = CreateCloud(options, clock, urlLauncher);
            apiClient = client;

            var services = new HeadServices(
                options, runModeHub.HomeDirectory, config, hooks, hookApproval, stores.RemoteProfiles, remoteEnvironments, mcp, stores.Checkpoints, setupConsent, stores.ConsentStore,
                worktrees, new AboutLauncher(), urlLauncher, display, runModeHub, runModes, stores.WorkspaceSets, cloud, apiClient, sshOptions);
            holder.Owner = services;
            created = true;

            // The services own the stores and the client from here on.
            stores = null;
            apiClient = null;
            return services;
        }
        finally
        {
            apiClient?.Dispose();
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

    /// <summary>Creates the cloud agents host and its HTTP client when an API address is configured; otherwise both are null.</summary>
    private static (ICloudAgentsHost? Host, HttpClient? Client) CreateCloud(HeadServicesOptions options, TimeProvider clock, IUrlLauncher urlLauncher)
    {
        var variable = options.GetVariable ?? (static name => Environment.GetEnvironmentVariable(name));
        if (ApiAddress(variable) is not { } address)
        {
            return (null, null);
        }

        var client = CreateApiClient(options.ApiHandler);
        var host = new ApiCloudAgentsHost(
            client,
            address,
            clock,
            _ => ValueTask.FromResult(Token(variable)),
            urlLauncher: urlLauncher,
            logger: options.LoggerFactory?.CreateLogger("OmpOS.Native.Cloud"));
        return (host, client);
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

        _cloudDisposable?.Dispose();
        _apiClient?.Dispose();
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

    private static string DefaultConfigDirectory(string stateRoot)
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        return string.IsNullOrEmpty(applicationData) ? Path.Combine(stateRoot, "config") : Path.Combine(applicationData, "OmpOS");
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
            var directory = Path.Combine(Path.GetTempPath(), "ompos-ssh-" + Environment.UserName);
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
