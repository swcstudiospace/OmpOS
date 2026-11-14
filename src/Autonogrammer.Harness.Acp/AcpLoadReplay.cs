using System.Text.Json;

namespace Autonogrammer.Harness.Acp;

/// <summary>The updates an agent replayed for one <c>session/load</c>.</summary>
/// <param name="SessionId">The loaded session.</param>
/// <param name="Updates">The <c>update</c> object of each replayed <c>session/update</c>, cloned, in wire order.</param>
/// <param name="Complete">False when the replay was longer than the session keeps; the updates then stop at the cap.</param>
public sealed record AcpLoadReplay(string SessionId, IReadOnlyList<JsonElement> Updates, bool Complete);
