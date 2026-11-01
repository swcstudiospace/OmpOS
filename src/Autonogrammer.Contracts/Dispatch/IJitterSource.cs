namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Half-open unit interval used by reconnect delay. Desktop and the controller client both take this seam so neither references the other.
/// </summary>
public interface IJitterSource
{
    /// <summary>Returns a value in the half-open interval <c>[0, 1)</c>.</summary>
    /// <returns>The next sample.</returns>
    double NextDouble();
}
