using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Remote;

/// <summary>One row of the New session picker: this computer, an ssh environment, or the cloud.</summary>
public sealed class NewSessionTargetOption : ObservableModel
{
    private RemoteEnvironmentStatus? _status;

    internal NewSessionTargetOption(string id, SessionTargetKind kind, string label, string detail, EnvironmentProfile? profile, string? unavailableReason)
    {
        Id = id;
        Kind = kind;
        Label = label;
        Detail = detail;
        Profile = profile;
        UnavailableReason = unavailableReason;
    }

    /// <summary>Gets the stable identifier: <c>local</c>, <c>cloud</c> or <c>ssh:&lt;profile id&gt;</c>.</summary>
    public string Id { get; }

    /// <summary>Gets where the option runs the agent.</summary>
    public SessionTargetKind Kind { get; }

    /// <summary>Gets the name shown on the row.</summary>
    public string Label { get; }

    /// <summary>Gets the second line: where it points.</summary>
    public string Detail { get; }

    /// <summary>Gets the environment of an ssh option, or <see langword="null"/>.</summary>
    public EnvironmentProfile? Profile { get; }

    /// <summary>Gets why the option cannot be chosen, or <see langword="null"/> when it can.</summary>
    public string? UnavailableReason { get; }

    /// <summary>Gets whether the option can be chosen.</summary>
    public bool IsAvailable => UnavailableReason is null;

    /// <summary>Gets the last known status of an ssh environment, or <see langword="null"/> for the other options and when nothing is known.</summary>
    public RemoteEnvironmentStatus? Status
    {
        get => _status;
        internal set
        {
            if (SetProperty(ref _status, value))
            {
                NotifyPropertyChanged(nameof(StatusLabel));
                NotifyPropertyChanged(nameof(AccessibleName));
            }
        }
    }

    /// <summary>Gets the words of the status, or an empty string.</summary>
    public string StatusLabel => _status is { } status ? RemoteStatusDescriber.LabelOf(status) : string.Empty;

    /// <summary>Gets the text a screen reader announces for the row: name, detail, status and why it is unavailable.</summary>
    public string AccessibleName =>
        string.Join(". ", new[] { Label, Detail, StatusLabel, UnavailableReason ?? string.Empty }.Where(part => part.Length > 0));
}
