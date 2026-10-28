namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// Implemented by an object that holds the <see cref="RemoteEnvironmentsViewModel"/> of the page, for example the Settings page adapter: the Remote
/// environments view accepts either the model itself or such a holder as its data context.
/// </summary>
public interface IRemoteEnvironmentsModelSource
{
    /// <summary>Gets the model the page shows.</summary>
    RemoteEnvironmentsViewModel Model { get; }
}
