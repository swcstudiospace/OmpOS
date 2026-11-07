using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop.Tests.Support;

/// <summary>A jitter source that always returns the same sample, so delays are exact in tests.</summary>
internal sealed class FixedJitterSource(double sample) : IJitterSource
{
    public double NextDouble() => sample;
}
