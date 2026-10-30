using System.ComponentModel;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Accounts;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views.Account;

/// <summary>
/// The account panel. Exactly one of the five states is visible. It is not placed in the window chrome. Enter submits and Escape cancels.
/// Countdowns update every second; the live region is renamed only at 30 seconds, 10 seconds and when another code is available.
/// </summary>
internal partial class AccountView : UserControl
{
    private AccountViewModel? _model;
    private DispatcherTimer? _clock;
    private string? _announced;

    /// <summary>Creates the panel with no model.</summary>
    public AccountView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Gets the email field.</summary>
    internal TextBox EmailField => EmailBox;

    /// <summary>Gets the code field. Its length is not fixed at six digits.</summary>
    internal TextBox CodeField => CodeBox;

    /// <summary>Gets the send button.</summary>
    internal Button SendCode => SendCodeButton;

    /// <summary>Gets the resend button.</summary>
    internal Button Resend => ResendButton;

    /// <summary>Gets the change-email button.</summary>
    internal Button ChangeEmail => ChangeEmailButton;

    /// <summary>Gets the cancel button.</summary>
    internal Button Cancel => CancelButton;

    /// <summary>Gets the devices button.</summary>
    internal Button Devices => DevicesButton;

    /// <summary>Gets the consent button.</summary>
    internal Button Remember => RememberButton;

    /// <summary>Gets the error message.</summary>
    internal TextBlock Error => ErrorMessage;

    /// <summary>Gets the live announcement. Its accessible name changes only at the throttled moments.</summary>
    internal TextBlock Announcement => LiveAnnouncement;

    /// <summary>Gets the static signing-in text used when motion is reduced.</summary>
    internal TextBlock SigningStatic => SigningInStatic;

    /// <summary>Gets the spinner. It is hidden when motion is reduced.</summary>
    internal Border Spinner => SigningInSpinner;

    /// <summary>Binds the panel to a model.</summary>
    /// <param name="model">The model. The view does not own it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public void Attach(AccountViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = model;
        DataContext = model;
        _model.PropertyChanged += OnModelChanged;
        EmailBox.Text = model.Email;
        CodeBox.Text = model.Code;
        EmailBox.TextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.Email = EmailBox.Text ?? string.Empty;
            }
        };
        CodeBox.TextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.Code = CodeBox.Text ?? string.Empty;
            }
        };
        _clock ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick -= OnTick;
        _clock.Tick += OnTick;
        _clock.Start();
        Apply();
    }

    private void OnTick(object? sender, EventArgs e) => _model?.RefreshClock();

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AccountViewModel.Phase) or nameof(AccountViewModel.ShowOffline) or nameof(AccountViewModel.ShowConsent)
            or nameof(AccountViewModel.ShowDevices) or nameof(AccountViewModel.Configured) or nameof(AccountViewModel.MustResend)
            or nameof(AccountViewModel.ShowLoopbackUnavailable) or nameof(AccountViewModel.ShowSessionNotRemembered)
            or nameof(AccountViewModel.ShowSignOutNotConfirmed) or nameof(AccountViewModel.ShowServiceChanged)
            or nameof(AccountViewModel.LiveAnnouncement))
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (_model is not { } model)
        {
            return;
        }

        SignedOutPanel.IsVisible = model.ShowSignedOut;
        AwaitingLinkPanel.IsVisible = model.ShowAwaitingLink;
        SigningInPanel.IsVisible = model.ShowSigningIn;
        SignedInPanel.IsVisible = model.ShowSignedIn;
        ErrorPanel.IsVisible = model.ShowError;
        EmailBox.IsVisible = model.Configured;
        SendCodeButton.IsVisible = model.Configured;
        NotConfiguredText.IsVisible = !model.Configured;
        LoopbackText.IsVisible = model.ShowLoopbackUnavailable;
        MustResendText.IsVisible = model.MustResend;
        CodeBox.IsEnabled = !model.MustResend;
        ResendButton.IsEnabled = model.ResendEnabled || model.MustResend;
        OfflineNotice.IsVisible = model.ShowOffline;
        SessionNotRememberedNotice.IsVisible = model.ShowSessionNotRemembered;
        RememberButton.IsVisible = model.ShowConsent;
        SignOutNotConfirmedNotice.IsVisible = model.ShowSignOutNotConfirmed;
        ServiceChangedNotice.IsVisible = model.ShowServiceChanged;
        DevicesButton.IsVisible = model.ShowDevices;
        var reduced = MotionProbe.IsReduced(this);
        SigningInSpinner.IsVisible = model.ShowSigningIn && !reduced;
        SigningInStatic.IsVisible = model.ShowSigningIn;
        if (reduced)
        {
            SigningInSpinner.Classes.Remove("spinner");
        }

        if (!string.Equals(_announced, model.LiveAnnouncement, StringComparison.Ordinal))
        {
            _announced = model.LiveAnnouncement;
            AutomationProperties.SetName(LiveAnnouncement, _announced ?? AccountCopy.ExpiryName);
            LiveAnnouncement.IsVisible = _announced is not null;
        }

        if (model.ShowAwaitingLink)
        {
            CodeBox.Focus();
        }
        else if (model.ShowError)
        {
            ErrorMessage.Focus();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_model is null || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = _model.SubmitFromEnterAsync(CancellationToken.None);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _ = _model.CancelFromEscapeAsync(CancellationToken.None);
        }
    }

    private void OnSendClick(object? sender, RoutedEventArgs e) => _ = _model?.SendCodeAsync(CancellationToken.None);

    private void OnResendClick(object? sender, RoutedEventArgs e) => _ = _model?.ResendCodeAsync(CancellationToken.None);

    private void OnChangeEmailClick(object? sender, RoutedEventArgs e) => _model?.ChangeEmail();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _ = _model?.CancelAsync(CancellationToken.None);

    private void OnRememberClick(object? sender, RoutedEventArgs e) => _ = _model?.RememberWithoutSystemProtectionAsync(CancellationToken.None);

    private void OnSignOutClick(object? sender, RoutedEventArgs e) => _ = _model?.SignOutAsync(CancellationToken.None);

    private void OnSignOutEverywhereClick(object? sender, RoutedEventArgs e) => _ = _model?.SignOutEverywhereAsync(CancellationToken.None);

    private void OnDismissErrorClick(object? sender, RoutedEventArgs e) => _ = _model?.DismissErrorAsync(CancellationToken.None);
}
