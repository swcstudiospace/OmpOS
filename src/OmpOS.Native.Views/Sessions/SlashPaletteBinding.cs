using Avalonia.Threading;
using OmpOS.Harness.Abstractions;
using OmpOS.Presentation.Commands;

namespace OmpOS.Native.Views.Sessions;

/// <summary>
/// Joins the <see cref="IOmpSessionCommands"/> of a running session to the <see cref="SlashPaletteModel"/> of its composer: the palette lists
/// what the engine advertises (next to the entries of a catalogue registry, which a chat session leaves empty), and a list the engine advertises later (also from another thread) reaches the palette
/// on the UI thread. The binding runs nothing: omp runs a slash command when the prompt that starts with it is sent (<c>#runPromptOrCommand</c>
/// of omp's <c>acp-agent.ts</c> tries a skill, then a built-in command, and passes any other text to the model as an ordinary prompt), so a
/// name the user types by hand that nobody advertised is sent as written.
/// </summary>
internal sealed class SlashPaletteBinding : IDisposable
{
    private readonly IOmpSessionCommands _control;
    private readonly Func<CancellationToken, Task>? _connect;
    private readonly CancellationTokenSource _lifetime = new();
    private int _disposed;
    private int _connecting;

    /// <summary>Creates the palette and shows what the control already reports.</summary>
    /// <param name="control">The commands of the session.</param>
    /// <param name="registry">The OmpOS catalogue the palette lists next to the engine's commands.</param>
    /// <param name="connect">
    /// Starts the engine session so that it advertises its commands, or null when the commands are known without it. It runs when the palette
    /// opens while the engine has advertised nothing yet, so a session that has not sent a prompt still lists its commands.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> or <paramref name="registry"/> is null.</exception>
    public SlashPaletteBinding(IOmpSessionCommands control, SlashCommandRegistry registry, Func<CancellationToken, Task>? connect)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(registry);

        _control = control;
        _connect = connect;
        Palette = new SlashPaletteModel(registry);
        _control.CommandsChanged += OnCommandsChanged;
        Palette.PropertyChanged += OnPaletteChanged;
        Push();
    }

    /// <summary>
    /// Gets the registry a chat session passes: empty, so the palette lists only what omp advertises. The shipped OmpOS catalogue
    /// (<see cref="SlashCommandCatalogue.Shipped"/>) has no handler wired in the head, and a row that does nothing when chosen is a dead control;
    /// <see cref="SlashPaletteModel"/> still merges catalogue entries for the day a handler is wired.
    /// </summary>
    public static SlashCommandRegistry EngineCommandsOnly { get; } = new([]);

    /// <summary>Gets the palette the composer shows.</summary>
    public SlashPaletteModel Palette { get; }

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
        if (string.Equals(e.PropertyName, nameof(SlashPaletteModel.IsOpen), StringComparison.Ordinal)
            && Palette.IsOpen
            && _connect is not null
            && _control.Commands.Count == 0
            && Interlocked.CompareExchange(ref _connecting, 1, 0) == 0)
        {
            _ = ConnectAsync(_connect);
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
