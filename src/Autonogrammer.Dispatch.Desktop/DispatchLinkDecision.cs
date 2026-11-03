using System.Runtime.InteropServices;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The connector's response to one ended link.</summary>
/// <param name="Action">Retry with jitter, retry after <paramref name="Delay"/>, or stop.</param>
/// <param name="State">The state to show.</param>
/// <param name="Delay">The fixed delay for <see cref="DispatchLinkAction.RetryAfter"/>; zero otherwise.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DispatchLinkDecision(DispatchLinkAction Action, DispatchConnectionState State, TimeSpan Delay);
