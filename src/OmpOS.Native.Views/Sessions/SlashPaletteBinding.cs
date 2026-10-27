using Avalonia.Threading;
using OmpOS.Harness.Abstractions;
using OmpOS.Presentation.Commands;

namespace OmpOS.Native.Views.Sessions;

/// <summary>
/// Joins the <see cref="IOmpSessionCommands"/> of a running session to the <see cref="SlashPaletteModel"/> of its composer: the palette lists
/// what the engine advertises next to the OmpOS catalogue and the custom commands, skills and subagents that discovery found, and a list the engine advertises later (also from another thread) reaches the palette
/// on the UI thread. The palette itself runs nothing. <see cref="DispatchAsync"/> decides what a send means: a native action runs through the head's <see cref="ISlashActionHost"/>, a discovered command,
/// skill or subagent becomes the prompt that is sent, and a name nobody knows is sent as written, because omp runs a slash command when the prompt that starts with it is sent (<c>#runPromptOrCommand</c>
/// of omp's <c>acp-agent.ts</c> tries a skill, then a built-in command, and passes any other text to the model as an ordinary prompt).
/// </summary>
internal sealed class SlashPaletteBinding : IDisposable
{
    /// <summary>The shortest time between two reads of the conventions that the opening of the palette starts.</summary>
    internal static readonly TimeSpan DiscoveryRefreshInterval = TimeSpan.FromSeconds(5);

    private readonly IOmpSessionCommands _control;
    private readonly Func<CancellationToken, Task>? _connect;
    private readonly ISlashDiscoverySource? _discoverySource;
    private readonly TimeProvider _time;
    private readonly SlashCommandDispatcher _dispatcher;
    private readonly CancellationTokenSource _lifetime = new();
    private ISlashActionHost? _host;
    private SlashDiscovery _discovery = SlashDiscovery.Empty;
    private DateTimeOffset? _lastDiscovery;
    private int _disposed;
    private int _connecting;
    private int _discovering;

    /// <summary>Creates the palette and shows what the control already reports.</summary>
    /// <param name="control">The commands of the session.</param>
    /// <param name="registry">The OmpOS catalogue the palette lists next to the engine's commands.</param>
    /// <param name="connect">
    /// Starts the engine session so that it advertises its commands, or null when the commands are known without it. It runs when the palette
    /// opens while the engine has advertised nothing yet, so a session that has not sent a prompt still lists its commands.
    /// </param>
    /// <param name="host">The head's action host for native commands, or null when the window runs none: native rows are then listed disabled.</param>
    /// <param name="discovery">The source of the custom commands, skills and subagents of the session's workspace, or null for none.</param>
    /// <param name="time">The clock that throttles the re-reads of the conventions, or null for the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> or <paramref name="registry"/> is null.</exception>
    public SlashPaletteBinding(
        IOmpSessionCommands control,
        SlashCommandRegistry registry,
        Func<CancellationToken, Task>? connect,
        ISlashActionHost? host = null,
        ISlashDiscoverySource? discovery = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(registry);

        _control = control;
        _connect = connect;
        _host = host;
        _discoverySource = discovery;
        _time = time ?? TimeProvider.System;
        _dispatcher = new SlashCommandDispatcher(new SlashCommandResolver(registry));
        Palette = new SlashPaletteModel(registry);
        Palette.SetActionHost(host);
        _control.CommandsChanged += OnCommandsChanged;
        Palette.PropertyChanged += OnPaletteChanged;
        Push();
    }

    /// <summary>
    /// Gets a registry with no command, for a session whose palette lists only what omp advertises (and what discovery finds). The shipped OmpOS catalogue
    /// (<see cref="SlashCommandCatalogue.Shipped"/>) is what a chat session passes: its native rows are listed disabled until the head's <see cref="ISlashActionHost"/> can run them.
    /// </summary>
    public static SlashCommandRegistry EngineCommandsOnly { get; } = new([]);

    /// <summary>Gets the palette the composer shows.</summary>
    public SlashPaletteModel Palette { get; }

    /// <summary>
    /// Reads the conventions of the session's workspace again and gives the result to the palette and the resolver. A source that fails keeps the previous result: the palette must not lose its rows
    /// because a file could not be read. It does nothing when the binding has no discovery source or is disposed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the palette has the new rows.</returns>
    public async Task RefreshDiscoveryAsync(CancellationToken cancellationToken)
    {
        if (_discoverySource is null || Volatile.Read(ref _disposed) != 0 || Interlocked.CompareExchange(ref _discovering, 1, 0) != 0)
        {
            return;
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            var found = await _discoverySource.LoadAsync(linked.Token).ConfigureAwait(true);
            _lastDiscovery = _time.GetUtcNow();
            if (Volatile.Read(ref _disposed) == 0)
            {
                _discovery = found;
                Palette.SetDiscovered(found);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The palette keeps the rows it has. A workspace that cannot be read shows its problem on the Settings pages.
        }
        finally
        {
            Interlocked.Exchange(ref _discovering, 0);
        }
    }

    /// <summary>Replaces the head's action host. The palette lists the native commands the new host can run as enabled and the others as disabled.</summary>
    /// <param name="host">The host, or null for none.</param>
    public void SetActionHost(ISlashActionHost? host)
    {
        _host = host;
        Palette.SetActionHost(host);
    }

    /// <summary>Decides what a send means and carries it out: runs a native action, expands a discovered command, hands a skill or subagent to the engine, or passes the text through unchanged.</summary>
    /// <param name="text">The text as typed.</param>
    /// <param name="cancellationToken">Cancels a native action.</param>
    /// <returns>Send a prompt, or show a sentence and send nothing.</returns>
    public Task<SlashDispatch> DispatchAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        var engine = _control.Commands.Select(static command => new SlashEngineCommand(command.Name, command.Description, command.InputHint)).ToArray();
        return _dispatcher.DispatchAsync(text, new SlashResolutionContext(engine, _discovery, _host), cancellationToken);
    }

    /// <summary>Stops following the control and cancels a connection that is still starting. The palette keeps what it lists.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _control.CommandsChanged -= OnCommandsChanged;
        Palette.PropertyChanged -= OnPaletteChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private void OnCommandsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Push();
        }
        else
        {
            Dispatcher.UIThread.Post(Push);
        }
    }

    private void OnPaletteChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SlashPaletteModel.IsOpen), StringComparison.Ordinal) || !Palette.IsOpen)
        {
            return;
        }

        if (_connect is not null && _control.Commands.Count == 0 && Interlocked.CompareExchange(ref _connecting, 1, 0) == 0)
        {
            _ = ConnectAsync(_connect);
        }

        if (_discoverySource is not null && (_lastDiscovery is not { } last || _time.GetUtcNow() - last >= DiscoveryRefreshInterval))
        {
            _ = RefreshDiscoveryAsync(CancellationToken.None);
        }
    }

    private async Task ConnectAsync(Func<CancellationToken, Task> connect)
    {
        try
        {
            await connect(_lifetime.Token).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The palette keeps listing the OmpOS commands. A session that cannot start reports that on the model chip and on the first prompt.
        }
        finally
        {
            Interlocked.Exchange(ref _connecting, 0);
        }
    }

    private void Push()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        Palette.SetEngineCommands([.. _control.Commands.Select(static command => new SlashEngineCommand(command.Name, command.Description, command.InputHint))]);
    }
}
