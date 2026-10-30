namespace Autonogrammer.Coordination;

/// <summary>
/// What a presence read observed. <see cref="Status.Unknown"/> means the store could not answer, which is not the same as
/// the device being offline.
/// </summary>
public readonly record struct DispatchPresence(DispatchPresence.Status State, string? Holder)
{
    /// <summary>The three answers a presence read can give.</summary>
    public enum Status
    {
        /// <summary>The device has a live holder.</summary>
        Online,

        /// <summary>No presence key exists.</summary>
        Offline,

        /// <summary>The store could not answer. Never treat this as offline.</summary>
        Unknown,
    }

    /// <summary>Gets the offline answer.</summary>
    public static DispatchPresence Offline { get; } = new(Status.Offline, null);

    /// <summary>Gets the unknown answer, used when the store cannot be reached.</summary>
    public static DispatchPresence Unknown { get; } = new(Status.Unknown, null);

    /// <summary>Returns an online answer for <paramref name="holder"/>.</summary>
    /// <param name="holder">The instance and connection that hold the device, as <c>instanceId:connectionId</c>.</param>
    /// <returns>The online snapshot.</returns>
    public static DispatchPresence Online(string holder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(holder);
        return new(Status.Online, holder);
    }
}
