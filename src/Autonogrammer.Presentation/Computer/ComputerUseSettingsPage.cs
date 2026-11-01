using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Presentation.Computer;

/// <summary>
/// The Computer Use settings page: provider disclosure, grants, denylist additions, kill chord, security phrase and audit.
/// </summary>
public sealed class ComputerUseSettingsPage : SettingsPageBase, IDisposable
{
    private readonly ComputerSettingsViewModel _model;
    private readonly ComputerPermissionAdvisor _advisor;

    /// <summary>Creates the page over the L1 ports.</summary>
    /// <param name="grants">Grant registry.</param>
    /// <param name="denylist">Denylist.</param>
    /// <param name="audit">Audit reader.</param>
    /// <param name="settings">Settings store.</param>
    /// <param name="killSwitch">Kill switch.</param>
    /// <param name="status">Permission status source.</param>
    /// <param name="time">Clock for the advisor poll.</param>
    /// <param name="indicatorVisible">Indicator probe.</param>
    public ComputerUseSettingsPage(
        IComputerGrantRegistry grants,
        IComputerDenylistStore denylist,
        IComputerAuditReader audit,
        IComputerUseSettingsStore settings,
        IKillSwitchControl killSwitch,
        IComputerStatusSource status,
        TimeProvider? time = null,
        Func<bool>? indicatorVisible = null)
        : base(SettingsSection.ComputerUse, ComputerUseCopy.Title, SettingsPageState.Loading)
    {
        _model = new ComputerSettingsViewModel(grants, denylist, audit, settings, killSwitch, indicatorVisible);
        _advisor = new ComputerPermissionAdvisor(status, time);
        _model.PropertyChanged += (_, _) => NotifyDirtyChanged();
    }

    /// <summary>Gets the settings model the view draws.</summary>
    public ComputerSettingsViewModel Model => _model;

    /// <summary>Gets the advisor.</summary>
    public IComputerPermissionAdvisor Advisor => _advisor;

    /// <summary>Gets the advisor panel title.</summary>
    public string AdvisorTitle => _advisor.Current.Title;

    /// <summary>Gets the advisor explanation.</summary>
    public string AdvisorBody => _advisor.Current.WhatItAllows;

    /// <summary>Gets the deep link, or empty.</summary>
    public string AdvisorDeepLink => _advisor.Current.DeepLink ?? string.Empty;

    /// <summary>Gets why the state is blocked, or empty.</summary>
    public string AdvisorWhyBlocked => _advisor.Current.WhyBlocked ?? string.Empty;

    /// <inheritdoc />
    public override bool IsDirty => _model.IsDirty;

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(LoadCoreAsync, cancellationToken);

    /// <inheritdoc />
    public override async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        await _model.SaveAsync(cancellationToken).ConfigureAwait(true);
        NotifyDirtyChanged();
        return true;
    }

    /// <inheritdoc />
    public override void Discard()
    {
        _model.Discard();
        base.Discard();
    }

    /// <inheritdoc />
    public override void Reset() => Discard();

    /// <inheritdoc />
    public void Dispose() => _advisor.Dispose();

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        await _model.LoadAsync(cancellationToken).ConfigureAwait(true);
        await _advisor.RefreshAsync(cancellationToken).ConfigureAwait(true);
        NotifyPropertyChanged(nameof(AdvisorTitle));
        NotifyPropertyChanged(nameof(AdvisorBody));
        NotifyPropertyChanged(nameof(AdvisorDeepLink));
        NotifyPropertyChanged(nameof(AdvisorWhyBlocked));
        NotifyPropertyChanged(nameof(Model));
    }
}
