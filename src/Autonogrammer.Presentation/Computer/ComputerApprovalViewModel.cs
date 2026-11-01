using System.Windows.Input;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Computer;

/// <summary>
/// Approval dialog model over <see cref="ComputerApprovalFacts"/> only. There is no model-text field.
/// Deny is the default. No timer ever allows. The dialog times out to deny after 120 seconds.
/// A remote answer is refused.
/// </summary>
public sealed class ComputerApprovalViewModel : ObservableModel, IDisposable
{
    /// <summary>Quoted title limit, including the quotes.</summary>
    public const int TitleLimit = 80;

    /// <summary>Host preview limit.</summary>
    public const int PreviewLimit = 40;

    private TaskCompletionSource<ComputerApprovalChoice> _choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private int _completed;

    /// <summary>Creates the model and starts the deny timeout.</summary>
    /// <param name="facts">Host-observed facts.</param>
    /// <param name="time">Clock. The timeout is <see cref="ComputerApprovalFacts.Timeout"/>.</param>
    /// <param name="crop">Optional 160 by 120 host crop. When set, the dialog shows the crop instead of agent text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="facts"/> is null.</exception>
    public ComputerApprovalViewModel(ComputerApprovalFacts facts, TimeProvider? time = null, ReadOnlyMemory<byte> crop = default)
    {
        ArgumentNullException.ThrowIfNull(facts);
        _time = time ?? TimeProvider.System;
        Facts = facts;
        QuotedTitle = Quote(facts.SanitizedTitle);
        ApplicationName = Plain(facts.ApplicationName, 80);
        AppIdentity = Plain(facts.AppIdentity, 120);
        HostActionWords = Plain(facts.HostActionWords, 160);
        ShowsCrop = !crop.IsEmpty;
        ShowsText = crop.IsEmpty;
        Crop = crop;
        var preview = Plain(facts.HostSanitizedPreview, PreviewLimit);
        AgentText = ComputerUseCopy.AgentTextLine(facts.HostSanitizedPreview?.Length ?? 0, preview);
        OriginLabel = FormatOrigin(facts.Origin, facts.DeviceName);
        SecurityPhrase = string.IsNullOrWhiteSpace(facts.SecurityPhrase) ? null : Plain(facts.SecurityPhrase, 80);
        Deny = new ChoiceCommand(() => Complete(ComputerApprovalChoice.Deny));
        AllowOnce = new ChoiceCommand(() => Complete(ComputerApprovalChoice.AllowOnce));
        AllowForSession = new ChoiceCommand(() => Complete(ComputerApprovalChoice.AllowForSession));
        _timer = _time.CreateTimer(OnTimeout, null, ComputerApprovalFacts.Timeout, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Gets the facts. Callers must not treat any member as model text.</summary>
    public ComputerApprovalFacts Facts { get; }

    /// <summary>Gets the sanitised quoted title, at most 80 characters.</summary>
    public string QuotedTitle { get; }

    /// <summary>Gets the application display name.</summary>
    public string ApplicationName { get; }

    /// <summary>Gets the application identity.</summary>
    public string AppIdentity { get; }

    /// <summary>Gets the action in host words.</summary>
    public string HostActionWords { get; }

    /// <summary>Gets a value indicating whether the dialog shows the host crop instead of agent text.</summary>
    public bool ShowsCrop { get; }

    /// <summary>Gets a value indicating whether the dialog shows agent text instead of the crop.</summary>
    public bool ShowsText { get; }

    /// <summary>Gets the host crop bytes, or empty.</summary>
    public ReadOnlyMemory<byte> Crop { get; }

    /// <summary>Gets the agent-text label.</summary>
    public string AgentTextLabel { get; } = ComputerUseCopy.AgentTextLabel;

    /// <summary>Gets the character count and the first 40 sanitised characters.</summary>
    public string AgentText { get; }

    /// <summary>Gets Local, or Dispatch with the device name.</summary>
    public string OriginLabel { get; }

    /// <summary>Gets the security phrase, or null.</summary>
    public string? SecurityPhrase { get; }

    /// <summary>Gets a value indicating whether a security phrase is shown.</summary>
    public bool HasSecurityPhrase => SecurityPhrase is not null;

    /// <summary>Gets the Deny command. It is the default.</summary>
    public ICommand Deny { get; }

    /// <summary>Gets Allow once. No timer calls this.</summary>
    public ICommand AllowOnce { get; }

    /// <summary>Gets Allow for this session. No timer calls this.</summary>
    public ICommand AllowForSession { get; }

    /// <summary>Gets a value indicating whether a choice was recorded. A remote answer does not set this.</summary>
    public bool IsCompleted => Volatile.Read(ref _completed) != 0;

    /// <summary>Waits for a local choice. Timeout and cancellation are deny.</summary>
    /// <param name="cancellationToken">Cancels the wait as deny.</param>
    /// <returns>The choice.</returns>
    public Task<ComputerApprovalChoice> WaitAsync(CancellationToken cancellationToken)
    {
        var wait = new TaskCompletionSource<ComputerApprovalChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (IsCompleted)
        {
            wait.SetResult(ComputerApprovalChoice.Deny);
            return wait.Task;
        }

        _choice = wait;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => Complete(ComputerApprovalChoice.Deny));
        }

        return wait.Task;
    }

    /// <summary>
    /// Refuses a choice that did not happen on this desktop. The wait stays open and the choice is not allow.
    /// </summary>
    /// <param name="choice">The remote choice. Ignored.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    public bool TryApplyRemote(ComputerApprovalChoice choice)
    {
        _ = choice;
        return IsCompleted && false;
    }

    /// <summary>Quotes and truncates a title to <see cref="TitleLimit"/> characters.</summary>
    /// <param name="title">The host title.</param>
    /// <returns>The quoted title.</returns>
    public static string Quote(string? title)
    {
        var plain = Plain(title, TitleLimit - 2);
        return "\"" + plain + "\"";
    }

    /// <summary>Formats the origin label. Dispatch includes the device name.</summary>
    /// <param name="origin">Local or Dispatch.</param>
    /// <param name="deviceName">Controller device name.</param>
    /// <returns>The label.</returns>
    public static string FormatOrigin(ComputerActionOrigin origin, string? deviceName)
    {
        if (origin != ComputerActionOrigin.Dispatch)
        {
            return ComputerUseCopy.LocalOrigin;
        }

        var device = Plain(string.IsNullOrWhiteSpace(deviceName) ? "unknown device" : deviceName, 40);
        return "Dispatch: " + device;
    }

    /// <summary>Formats the indicator's remote label. Empty when the origin is local.</summary>
    /// <param name="origin">Local or Dispatch.</param>
    /// <param name="deviceName">Controller device name.</param>
    /// <returns>The label, or empty.</returns>
    public static string FormatRemote(ComputerActionOrigin origin, string? deviceName)
    {
        if (origin != ComputerActionOrigin.Dispatch)
        {
            return string.Empty;
        }

        var device = Plain(string.IsNullOrWhiteSpace(deviceName) ? "unknown device" : deviceName, 40);
        return "Remote (Dispatch): " + device;
    }

    /// <inheritdoc />
    public void Dispose() => _timer.Dispose();

    private void OnTimeout(object? state) => Complete(ComputerApprovalChoice.Deny);

    private void Complete(ComputerApprovalChoice choice)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        _timer.Dispose();
        _choice.TrySetResult(choice);
        NotifyPropertyChanged(nameof(IsCompleted));
    }

    private static string Plain(string? value, int limit)
    {
        if (string.IsNullOrEmpty(value) || limit <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[Math.Min(value.Length, limit)];
        var count = 0;
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                continue;
            }

            buffer[count++] = character;
            if (count == buffer.Length)
            {
                break;
            }
        }

        return new string(buffer, 0, count);
    }

    private sealed class ChoiceCommand : ICommand
    {
        private readonly Action _execute;

        public ChoiceCommand(Action execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
