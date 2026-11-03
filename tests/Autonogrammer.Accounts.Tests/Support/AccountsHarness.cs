using Autonogrammer.Contracts.Accounts;

namespace Autonogrammer.Accounts.Tests.Support;

/// <summary>Everything one test needs, wired over the fake service and a manual clock.</summary>
internal sealed class AccountsHarness : IDisposable
{
    private AccountsHarness(bool magicLink, bool configured, Func<Journal, IRefreshGuard>? guardFactory, RecordingSecretStore? store)
    {
        Time = new ManualTimeProvider();
        Journal = new Journal();
        Handler = new FakeGoTrueHandler(Time);
        Http = new HttpClient(Handler);
        Sink = new LogSink();
        Store = store ?? new RecordingSecretStore(Journal);
        Options = GoTrueOptions.Create(new AuthConfigResponse(
            "oidc",
            "https://fake-project.supabase.co/auth/v1",
            "authenticated",
            new Uri(FakeGoTrueHandler.ProjectUrl),
            FakeGoTrueHandler.PublishableKey,
            new AuthMethods(EmailCode: true, MagicLink: magicLink, Providers: [], Wallet: false),
            new AuthEmailCode(ExpirySeconds: 3600, ResendCooldownSeconds: 60),
            SignupEmailHasCode: false)).Options!;
        Client = new GoTrueClient(Http, Options, Time, new CapturingLogger<GoTrueClient>(Sink));
        Provider = configured ? AccountSessionManager.FixedClient(Client) : AccountSessionManager.FixedClient(null);
        Manager = new AccountSessionManager(Store, Provider, Time, guardFactory?.Invoke(Journal), new CapturingLogger<AccountSessionManager>(Sink))
        {
            Jitter = static interval => interval,
        };
        Coordinator = new SignInCoordinator(Manager, Provider, Time, new CapturingLogger<SignInCoordinator>(Sink));
        Manager.StateChanged += (_, e) => Journal.Add("publish:" + e.State.Phase + "/" + e.State.Connectivity + "/" + e.State.Notices);
    }

    /// <summary>Gets the manual clock.</summary>
    public ManualTimeProvider Time { get; }

    /// <summary>Gets the ordered journal of store, guard and state events.</summary>
    public Journal Journal { get; }

    /// <summary>Gets the fake service.</summary>
    public FakeGoTrueHandler Handler { get; }

    /// <summary>Gets the HTTP client over the fake.</summary>
    public HttpClient Http { get; }

    /// <summary>Gets the captured log lines.</summary>
    public LogSink Sink { get; }

    /// <summary>Gets the secret store.</summary>
    public RecordingSecretStore Store { get; }

    /// <summary>Gets the options of the client.</summary>
    public GoTrueOptions Options { get; }

    /// <summary>Gets the client.</summary>
    public GoTrueClient Client { get; }

    /// <summary>Gets the deferred client provider.</summary>
    public Func<CancellationToken, ValueTask<GoTrueClient?>> Provider { get; }

    /// <summary>Gets the session manager.</summary>
    public AccountSessionManager Manager { get; }

    /// <summary>Gets the sign-in coordinator.</summary>
    public SignInCoordinator Coordinator { get; }

    /// <summary>Creates a harness.</summary>
    /// <param name="magicLink">Whether the project offers the magic link.</param>
    /// <param name="configured">False makes the client provider answer null.</param>
    /// <param name="guardFactory">Builds the refresh guard from the journal, or null for none.</param>
    /// <param name="store">A store to use instead of a new one.</param>
    /// <returns>The harness.</returns>
    public static AccountsHarness Create(bool magicLink = false, bool configured = true, Func<Journal, IRefreshGuard>? guardFactory = null, RecordingSecretStore? store = null) =>
        new(magicLink, configured, guardFactory, store);

    /// <summary>Writes the stored session of a person who signed in before, using a refresh token the fake knows.</summary>
    /// <param name="session">A session the fake issued.</param>
    /// <param name="issuer">The issuer to record, or null for the harness project.</param>
    /// <returns>A task that completes once it is stored.</returns>
    public Task SeedStoredSessionAsync(GoTrueSession session, string? issuer = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Store.SeedAsync(
            $$"""{"schema":1,"issuer":"{{issuer ?? Options.Issuer}}","refreshToken":"{{session.RefreshToken.Reveal()}}","userId":"{{session.UserId}}","email":"{{session.Email}}"}""");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Manager.Dispose();
        Http.Dispose();
        Handler.Dispose();
    }
}
