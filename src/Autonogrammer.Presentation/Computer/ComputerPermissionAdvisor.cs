using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Computer;


/// <summary>
/// Advisor over <see cref="IComputerStatusSource"/>. Every <see cref="ComputerAvailability"/> maps to one panel.
/// The poll is one-second ticks for at most five minutes. A missing permission never falls back to another mechanism.
/// </summary>
public sealed class ComputerPermissionAdvisor : ObservableModel, IComputerPermissionAdvisor, IDisposable
{
    /// <summary>Poll interval while the panel is open.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>Longest the poll runs.</summary>
    public static readonly TimeSpan PollLimit = TimeSpan.FromMinutes(5);

    /// <summary>macOS Screen Recording privacy pane.</summary>
    public const string ScreenRecordingDeepLink = "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture";

    /// <summary>macOS Accessibility privacy pane.</summary>
    public const string AccessibilityDeepLink = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility";

    /// <summary>Windows privacy settings.</summary>
    public const string WindowsPrivacyDeepLink = "ms-settings:privacy";

    private readonly IComputerStatusSource _status;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private ITimer? _timer;
    private Task? _refresh;
    private DateTimeOffset _openedAt;
    private ComputerPermissionPanel _current = For(ComputerAvailability.Disabled);

    /// <summary>Creates an advisor.</summary>
    /// <param name="status">Status port. The head adapter wraps the backend.</param>
    /// <param name="time">Clock for the poll. Null uses <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="status"/> is null.</exception>
    public ComputerPermissionAdvisor(IComputerStatusSource status, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(status);
        _status = status;
        _timeProvider = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ComputerPermissionPanel Current => _current;

    /// <inheritdoc />
    public bool IsPolling
    {
        get
        {
            lock (_gate)
            {
                return _timer is not null;
            }
        }
    }

    /// <summary>Maps one availability state. Unmapped values throw.</summary>
    /// <param name="availability">The state.</param>
    /// <returns>The panel. Blocked states have why-blocked text. Available has a settings deep link.</returns>
    public static ComputerPermissionPanel For(ComputerAvailability availability)
    {
        var id = ComputerAvailabilityRemediation.For(availability);
        return availability switch
        {
            ComputerAvailability.Available => Panel("Computer Use can arm", "This desktop can capture and inject while the indicator is visible.", "autonogrammer://settings/computer-use", null, id),
            ComputerAvailability.Disabled => Blocked("Computer Use is off", "Turn it on in Settings after you opt in. Nothing else is used in its place.", id),
            ComputerAvailability.UnsupportedPlatform => Blocked("This computer has no desktop session", "Computer Use stays unavailable. No other mechanism is used.", id),
            ComputerAvailability.WaylandUnsupported => Blocked("Wayland is not supported", "Including XWayland. Computer Use stays off. No other compositor is used.", id),
            ComputerAvailability.NoDisplay => Blocked("No display", "X11 has no display. Computer Use stays off. No other capture path is used.", id),
            ComputerAvailability.PermissionMissing => Panel("A permission is missing", "Grant Screen Recording and Accessibility in System Settings. A missing permission never falls back to another mechanism.", ScreenRecordingDeepLink, "Open System Settings at the Privacy pane. Restart may be needed. macOS may ask again each month.", id),
            ComputerAvailability.InputInjectionUnavailable => Blocked("Input injection is unavailable", "X11 cannot post input. Computer Use stays off. No other injection path is used.", id),
            ComputerAvailability.KillSwitchUnavailable => Blocked("The kill switch did not register", "Computer Use stays off until the chord registers. No action runs without it.", id),
            ComputerAvailability.IndicatorUnavailable => Blocked("The indicator is not visible", "The gate denies while the indicator is missing or minimised.", id),
            ComputerAvailability.AuditUnavailable => Blocked("The audit log cannot be written", "Computer Use stays off. Actions are not injected without an audit write.", id),
            ComputerAvailability.SecureDesktop => Blocked("The secure desktop is active", "Windows secure desktop blocks injection. Wait until it closes. No other input path is used.", id),
            _ => throw new ArgumentOutOfRangeException(nameof(availability)),
        };
    }

    /// <summary>Maps one permission status. A missing grant names the System Settings pane and no fallback.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The panel.</returns>
    public static ComputerPermissionPanel ForPermission(ComputerPermissionStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var link = status.Kind == ComputerPermissionKind.Accessibility ? AccessibilityDeepLink : ScreenRecordingDeepLink;
        if (status.Granted)
        {
            return Panel(status.Kind + " is granted", "The operating system reports this permission is granted.", link, null, status.MessageId);
        }

        var name = status.Kind == ComputerPermissionKind.Accessibility ? "Accessibility" : "Screen Recording";
        return Panel(
            name + " is not granted",
            name + " lets Computer Use post input or capture the screen. It is not granted. No other mechanism is used.",
            link,
            "Open System Settings at the Privacy pane. Restart may be needed. macOS may ask again each month.",
            status.MessageId);
    }

    /// <summary>Windows elevation text. Not a fallback for a missing permission.</summary>
    /// <returns>The panel.</returns>
    public static ComputerPermissionPanel ElevatedTarget() =>
        Blocked("The target is elevated", "Windows UIPI blocks input to an elevated window. Computer Use does not raise itself to match it.", "computer.permission.elevated-target");

    /// <inheritdoc />
    public void Open()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _openedAt = _timeProvider.GetUtcNow();
            _timer = _timeProvider.CreateTimer(OnTick, null, PollInterval, PollInterval);
        }

        NotifyPropertyChanged(nameof(IsPolling));
    }

    /// <inheritdoc />
    public void Close()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }

        NotifyPropertyChanged(nameof(IsPolling));
    }

    /// <inheritdoc />
    public async ValueTask RefreshAsync(CancellationToken cancellationToken)
    {
        var availability = await _status.GetAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        var panel = For(availability);
        if (availability == ComputerAvailability.PermissionMissing)
        {
            var permissions = await _status.GetPermissionStatusAsync(cancellationToken).ConfigureAwait(false);
            var missing = permissions.FirstOrDefault(static item => !item.Granted);
            if (missing is not null)
            {
                panel = ForPermission(missing);
            }
        }

        _current = panel;
        NotifyPropertyChanged(nameof(Current));
    }

    /// <inheritdoc />
    public void Dispose() => Close();

    private void OnTick(object? state)
    {
        if (_timeProvider.GetUtcNow() - _openedAt >= PollLimit)
        {
            Close();
            return;
        }

        var pending = RefreshAsync(CancellationToken.None);
        if (!pending.IsCompletedSuccessfully)
        {
            _refresh = pending.AsTask();
        }
    }

    private static ComputerPermissionPanel Blocked(string title, string why, string messageId) =>
        Panel(title, why, null, why, messageId);

    private static ComputerPermissionPanel Panel(string title, string what, string? link, string? why, string messageId) =>
        new()
        {
            Title = title,
            WhatItAllows = what,
            DeepLink = link,
            WhyBlocked = why,
            MessageId = messageId,
        };
}
