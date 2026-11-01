using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Computer;

/// <summary>Opt-in dialog model. Accepting does not approve an action.</summary>
public sealed class ComputerOptInModel : ObservableModel
{
    private bool? _accepted;

    /// <summary>Gets the disclosure.</summary>
    public string Disclosure { get; } = ComputerUseCopy.ProviderDisclosure;

    /// <summary>Gets whether the user opted in. Null until they choose.</summary>
    public bool? Accepted => _accepted;

    /// <summary>Opts in. Does not allow an action.</summary>
    public void Accept()
    {
        _accepted = true;
        NotifyPropertyChanged(nameof(Accepted));
    }

    /// <summary>Keeps Computer Use off.</summary>
    public void Decline()
    {
        _accepted = false;
        NotifyPropertyChanged(nameof(Accepted));
    }
}
