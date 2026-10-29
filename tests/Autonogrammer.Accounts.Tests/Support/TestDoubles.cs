using System.Collections.Concurrent;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts.Tests.Support;

#pragma warning disable MA0048, VSTHRD003 // Test doubles that serve only this test project stay together in one file; the clock driver awaits tasks its caller owns.

/// <summary>An ordered record of events from several test doubles, so a test can assert what happened before what.</summary>
internal sealed class Journal
{
    private readonly Lock _gate = new();
    private readonly List<string> _entries = [];

    /// <summary>Gets a snapshot of the entries in order.</summary>
    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>Adds an entry.</summary>
    /// <param name="entry">The entry.</param>
    public void Add(string entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }
}

/// <summary>Collects every log line, with its event id and rendered message, from every <see cref="CapturingLogger{T}"/> that shares it.</summary>
internal sealed class LogSink
{
    private readonly ConcurrentQueue<string> _lines = new();

    /// <summary>Gets the lines captured so far.</summary>
    public IReadOnlyList<string> Lines => [.. _lines];

    /// <summary>Adds a line.</summary>
    /// <param name="line">The line.</param>
    public void Add(string line) => _lines.Enqueue(line);
}

/// <summary>A logger that records the event id, level, rendered message and exception text of everything written to it.</summary>
/// <typeparam name="T">The category type.</typeparam>
internal sealed class CapturingLogger<T>(LogSink sink) : ILogger<T>
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        string stateText = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? string.Join(';', pairs.Select(pair => pair.Key + "=" + pair.Value))
            : string.Empty;
        sink.Add($"{eventId.Id}|{logLevel}|{formatter(state, exception)}|{stateText}|{exception}");
    }
}

/// <summary>
/// An in-memory <see cref="ISecretStore"/> that journals its operations and can be told to fail, so a test can assert that the rotated refresh token is
/// written before the new session is published.
/// </summary>
internal sealed class RecordingSecretStore(Journal? journal = null) : ISecretStore
{
    private readonly InMemorySessionSecretStore _inner = new();
    private readonly ConcurrentQueue<string> _events = new();

    /// <summary>Gets or sets a value indicating whether <see cref="SetAsync"/> throws.</summary>
    public bool FailSet { get; set; }

    /// <summary>Gets or sets a callback run at the start of every write, before it takes effect.</summary>
    public Action? BeforeSet { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="GetAsync"/> throws.</summary>
    public bool FailGet { get; set; }

    /// <summary>Gets the operations seen so far, as <c>set:name</c>, <c>get:name</c> and <c>remove:name</c>.</summary>
    public IReadOnlyList<string> Events => [.. _events];

    /// <summary>Gets the number of <see cref="GetAsync"/> calls.</summary>
    public int GetCount => _events.Count(e => e.StartsWith("get:", StringComparison.Ordinal));

    /// <inheritdoc />
    public Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken)
    {
        Record("set:" + name);
        BeforeSet?.Invoke();
        return FailSet ? Task.FromException(new SecretStoreException("The test store refuses writes.")) : _inner.SetAsync(name, secret, cancellationToken);
    }

    /// <inheritdoc />
    public Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken)
    {
        Record("get:" + name);
        return FailGet ? Task.FromException<SecretString?>(new SecretStoreException("The test store refuses reads.")) : _inner.GetAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        Record("remove:" + name);
        return _inner.RemoveAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken) => _inner.ListNamesAsync(cancellationToken);

    /// <summary>Reads the stored session text for an assertion, without journaling it.</summary>
    /// <returns>The JSON, or null when nothing is stored.</returns>
    public async Task<string?> ReadSessionJsonAsync()
    {
        SecretString? secret = await _inner.GetAsync(AccountSessionManager.SecretName, CancellationToken.None).ConfigureAwait(false);
        return secret?.Reveal();
    }

    /// <summary>Writes a stored session as another process or an earlier run would have, without journaling it.</summary>
    /// <param name="json">The stored JSON.</param>
    /// <returns>A task that completes once it is stored.</returns>
    public Task SeedAsync(string json) => _inner.SetAsync(AccountSessionManager.SecretName, new SecretString(json), CancellationToken.None);

    private void Record(string entry)
    {
        _events.Enqueue(entry);
        journal?.Add(entry);
    }
}

/// <summary>A guard that journals when it is acquired and released, and runs a callback while holding, so a test can prove the order of events.</summary>
internal sealed class RecordingRefreshGuard(Journal journal, Func<ValueTask>? whileHeld = null) : IRefreshGuard
{
    /// <inheritdoc />
    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        journal.Add("acquire");
        if (whileHeld is not null)
        {
            await whileHeld().ConfigureAwait(false);
        }

        return new Handle(journal);
    }

    private sealed class Handle(Journal journal) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            journal.Add("release");
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>Drives a <see cref="ManualTimeProvider"/> while a task waits on it.</summary>
internal static class TimeDriver
{
    /// <summary>Advances the clock in steps until the task completes.</summary>
    /// <param name="time">The clock.</param>
    /// <param name="task">The task that waits on timers.</param>
    /// <param name="step">How far to move per step.</param>
    /// <returns>A task that completes with <paramref name="task"/>.</returns>
    public static async Task AdvanceUntilAsync(this ManualTimeProvider time, Task task, TimeSpan step)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(task);
        for (int i = 0; i < 3000 && !task.IsCompleted; i++)
        {
            time.Advance(step);
            _ = await Task.WhenAny(task, Task.Delay(5)).ConfigureAwait(false);
        }

        await task.ConfigureAwait(false);
    }

    /// <summary>Advances the clock in steps until a condition holds.</summary>
    /// <param name="time">The clock.</param>
    /// <param name="condition">The condition.</param>
    /// <param name="step">How far to move per step.</param>
    /// <returns>A task that completes when it holds; it fails after the clock moved 200 steps.</returns>
    public static async Task AdvanceUntilAsync(this ManualTimeProvider time, Func<bool> condition, TimeSpan step)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(condition);
        for (int i = 0; i < 200 && !condition(); i++)
        {
            time.Advance(step);
            await Task.Delay(5).ConfigureAwait(false);
        }

        if (!condition())
        {
            throw new TimeoutException("The condition did not hold after the clock advanced.");
        }
    }

    /// <summary>Waits, in real time, until a condition holds.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns>A task that completes when it holds; it fails after about ten seconds.</returns>
    public static async Task UntilAsync(Func<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        for (int i = 0; i < 2000 && !condition(); i++)
        {
            await Task.Delay(5).ConfigureAwait(false);
        }

        if (!condition())
        {
            throw new TimeoutException("The condition did not hold in time.");
        }
    }
}

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
            new AuthMethods(EmailCode: true, MagicLink: magicLink),
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
