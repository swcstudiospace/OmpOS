using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Presentation.Settings;

namespace OmpOS.UI.Components.Settings;

/// <summary>
/// The Hooks page of the web head: the hooks the workspace's hook files define, the files they come from and the problems found in them,
/// read-only. The page lists; it neither runs nor changes a hook.
/// </summary>
public sealed class HooksListPage : SettingsPageBase
{
    private readonly IWorkspaceSettingsReader _reader;
    private HooksResponse? _hooks;

    /// <summary>Initializes a new instance of the <see cref="HooksListPage"/> class.</summary>
    /// <param name="reader">Where the hooks are read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    public HooksListPage(IWorkspaceSettingsReader reader)
        : base(SettingsSection.Hooks, SettingsWebCopy.HooksTitle, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    /// <summary>Gets the hooks, or an empty list before the page has loaded.</summary>
    public IReadOnlyList<WorkspaceHookInfo> Hooks => _hooks?.Hooks ?? [];

    /// <summary>Gets the hook files the hooks were read from.</summary>
    public IReadOnlyList<WorkspaceHookSourceInfo> Sources => _hooks?.Sources ?? [];

    /// <summary>Gets the problems found while reading the hook files.</summary>
    public IReadOnlyList<WorkspaceHookDiagnosticInfo> Diagnostics => _hooks?.Diagnostics ?? [];

    /// <summary>Gets a value indicating whether the page loaded and no hook is defined.</summary>
    public bool IsEmpty => State == SettingsPageState.Ready && Hooks.Count == 0;

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(
        async token =>
        {
            var hooks = await _reader.GetHooksAsync(token).ConfigureAwait(true);
            if (!hooks.Available)
            {
                throw new SettingsSourceException(hooks.Reason ?? SettingsWebCopy.HooksUnavailable);
            }

            _hooks = hooks;
            NotifyPropertyChanged(nameof(Hooks));
            NotifyPropertyChanged(nameof(Sources));
            NotifyPropertyChanged(nameof(Diagnostics));
            NotifyPropertyChanged(nameof(IsEmpty));
        },
        cancellationToken);

    /// <summary>Returns the text of the command a hook runs: the command, or the arguments joined by spaces.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>The command text, or an empty string when the hook runs no command.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public static string CommandText(WorkspaceHookInfo hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        if (hook.Args is { Count: > 0 } args)
        {
            return string.Join(' ', args);
        }

        return hook.Command ?? string.Empty;
    }
}
