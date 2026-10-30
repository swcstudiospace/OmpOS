using System.Security.Cryptography;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary><see cref="IJitterSource"/> backed by <see cref="RandomNumberGenerator"/>. Samples stay in <c>[0, 1)</c>.</summary>
public sealed class SystemJitterSource : IJitterSource
{
    /// <inheritdoc />
    public double NextDouble()
    {
        var sample = RandomNumberGenerator.GetInt32(0, int.MaxValue);
        return sample / (double)int.MaxValue;
    }
}
