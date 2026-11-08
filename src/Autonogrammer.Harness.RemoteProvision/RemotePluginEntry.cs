using System.Text.Json.Nodes;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>One plugin entry of the remote bundle record.</summary>
/// <param name="Dir">The plugin directory name under the remote plugins directory.</param>
/// <param name="Commit">The 40-character lowercase commit the directory was installed at.</param>
/// <param name="Entry">The entry as the record holds it, so a rewrite keeps the fields it does not change.</param>
public sealed record RemotePluginEntry(string Dir, string Commit, JsonObject Entry);
