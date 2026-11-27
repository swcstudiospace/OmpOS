namespace Autonogrammer.Native.Engine;

/// <summary>
/// Which wire transport one session's engine speaks to omp. <see cref="Acp"/> is the shipped default everywhere;
/// <see cref="Rpc"/> is the Phase 80 opt-in for plain local sessions only and becomes the default no earlier than Phase 84.
/// </summary>
internal enum EngineTransport
{
    /// <summary>The ACP transport (<c>omp acp</c>). The shipped default.</summary>
    Acp,

    /// <summary>The RPC transport (<c>omp --mode rpc</c>). Opt-in for plain local sessions; see <see cref="EngineCompositionOptions.Transport"/>.</summary>
    Rpc,
}
