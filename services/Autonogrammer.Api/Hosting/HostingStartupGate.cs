namespace Autonogrammer.Api.Hosting;

/// <summary>One-way latch that opens once the session store has been reachable. The startup probe reports it.</summary>
internal sealed class HostingStartupGate
{
    private int _open;

    /// <summary>Gets a value indicating whether the gate has opened.</summary>
    public bool IsOpen => Volatile.Read(ref _open) == 1;

    /// <summary>Opens the gate. Calling it again has no effect.</summary>
    public void Open() => Volatile.Write(ref _open, 1);
}
