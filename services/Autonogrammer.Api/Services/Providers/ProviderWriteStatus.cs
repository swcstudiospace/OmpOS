namespace Autonogrammer.Api.Services.Providers;

/// <summary>What happened to a write to omp.</summary>
internal enum ProviderWriteStatus
{
    /// <summary>omp now uses the change.</summary>
    Done = 0,

    /// <summary>omp or the provider rules refused the change; the message says why.</summary>
    Rejected,

    /// <summary>The id belongs to a block Autonogrammer does not own.</summary>
    Conflict,

    /// <summary>omp could not be run or its files could not be written.</summary>
    Unavailable,
}
