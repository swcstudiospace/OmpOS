#pragma warning disable CA1822
using System.Windows.Input;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Settings.Plugins;

/// <summary>The plugin-bundle section: install, enable, and per-skill switches. Failures become banner or row text.</summary>
public sealed class BundleSectionModel : ObservableModel
{
    /// <summary>The limit copy shown on the screen that lists hooks.</summary>
    public const string HooksListedNotRun = "hooks listed, not run";

    private const string UnexpectedFailure = "The bundle change could not be completed.";

    private readonly IBundleManagementSource _source;
    private string _installPath = string.Empty;
    private string _banner = string.Empty;
    private IReadOnlyList<BundleRowModel> _rows = [];

    /// <summary>Initializes a new instance of the <see cref="BundleSectionModel"/> class.</summary>
    /// <param name="source">The management source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public BundleSectionModel(IBundleManagementSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        InstallCommand = new BundleActionCommand(() => InstallAsync(CancellationToken.None), ReportUnexpectedFailure);
    }

    /// <summary>Gets or sets the folder or zip path to install.</summary>
    public string InstallPath
    {
        get => _installPath;
        set => SetProperty(ref _installPath, value);
    }

    /// <summary>Gets the banner text. A refused install is shown here and is never thrown.</summary>
    public string Banner => _banner;

    /// <summary>Gets the installed bundles.</summary>
    public IReadOnlyList<BundleRowModel> Rows => _rows;

    /// <summary>Gets the limit copy. Hooks are listed and never run.</summary>
    public string LimitCopy => HooksListedNotRun;

    /// <summary>Gets the command that installs <see cref="InstallPath"/>.</summary>
    public ICommand InstallCommand { get; }

    /// <summary>Loads the rows from the source.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the rows are current.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var snapshots = await _source.ListAsync(cancellationToken).ConfigureAwait(true);
        _rows = [.. snapshots.Select(ToRow)];
        NotifyPropertyChanged(nameof(Rows));
    }

    /// <summary>Installs <see cref="InstallPath"/>. A <see cref="BundleManagementException"/> becomes <see cref="Banner"/>.</summary>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <returns>A task that completes when the install finished or was shown as a banner.</returns>
    public async Task InstallAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = await _source.InstallAsync(_installPath, cancellationToken).ConfigureAwait(true);
            SetBanner(string.Empty);
            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (BundleManagementException exception)
        {
            SetBanner(exception.Message);
        }
    }

    /// <summary>Enables or disables a bundle. A refusal becomes <see cref="Banner"/>.</summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="enabled">The new flag.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the row matches the store or the banner shows the refusal.</returns>
    public async Task SetBundleEnabledAsync(string bundleId, bool enabled, CancellationToken cancellationToken)
    {
        try
        {
            await _source.SetBundleEnabledAsync(bundleId, enabled, cancellationToken).ConfigureAwait(true);
            Row(bundleId)?.Apply(enabled);
            SetBanner(string.Empty);
        }
        catch (BundleManagementException exception)
        {
            SetBanner(exception.Message);
        }
        finally
        {
            Row(bundleId)?.ShowStoredEnabled();
        }
    }

    /// <summary>Enables or disables one skill. A refusal becomes <see cref="Banner"/>.</summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="skillId">The skill id.</param>
    /// <param name="enabled">The new flag.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the switch matches the store or the banner shows the refusal.</returns>
    public async Task SetSkillEnabledAsync(string bundleId, string skillId, bool enabled, CancellationToken cancellationToken)
    {
        try
        {
            await _source.SetSkillEnabledAsync(bundleId, skillId, enabled, cancellationToken).ConfigureAwait(true);
            Row(bundleId)?.Skills.FirstOrDefault(skill => string.Equals(skill.Id, skillId, StringComparison.Ordinal))?.SetEnabled(enabled);
            SetBanner(string.Empty);
        }
        catch (BundleManagementException exception)
        {
            SetBanner(exception.Message);
        }
        finally
        {
            Row(bundleId)?.Skills.FirstOrDefault(skill => string.Equals(skill.Id, skillId, StringComparison.Ordinal))?.ShowStoredEnabled();
        }
    }

    private BundleRowModel ToRow(BundleSnapshot snapshot)
    {
        var skills = snapshot.Skills.Select(skill => new BundleSkillRowModel(
            snapshot.Id,
            skill.Id,
            skill.Enabled,
            new BundleActionCommand(() => ToggleSkillAsync(snapshot.Id, skill.Id), ReportUnexpectedFailure))).ToArray();
        return new BundleRowModel(
            snapshot,
            new BundleActionCommand(() => ToggleBundleAsync(snapshot.Id), ReportUnexpectedFailure),
            skills);
    }

    private async Task ToggleBundleAsync(string bundleId)
    {
        if (Row(bundleId) is { } row)
        {
            await SetBundleEnabledAsync(bundleId, !row.Enabled, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async Task ToggleSkillAsync(string bundleId, string skillId)
    {
        if (Row(bundleId)?.Skills.FirstOrDefault(skill => string.Equals(skill.Id, skillId, StringComparison.Ordinal)) is { } skill)
        {
            await SetSkillEnabledAsync(bundleId, skillId, !skill.Enabled, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private void ReportUnexpectedFailure(Exception exception) =>
        SetBanner(string.IsNullOrWhiteSpace(exception.Message) ? UnexpectedFailure : exception.Message);

    private BundleRowModel? Row(string bundleId) =>
        _rows.FirstOrDefault(row => string.Equals(row.Id, bundleId, StringComparison.Ordinal));

    private void SetBanner(string banner)
    {
        _banner = banner;
        NotifyPropertyChanged(nameof(Banner));
    }

    private sealed class BundleActionCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Action<Exception> _onFault;

        public BundleActionCommand(Func<Task> execute, Action<Exception> onFault)
        {
            _execute = execute;
            _onFault = onFault;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _ = RunAsync();

        private async Task RunAsync()
        {
            try
            {
                await _execute().ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _onFault(exception);
            }
        }
    }
}
