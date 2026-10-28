using System.Text.Json.Nodes;

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Edits the read keys of an Autonogrammer <c>sandbox.json</c>. Other keys are left alone. Only files Autonogrammer owns can be edited.</summary>
public static class SandboxFileEditor
{
    /// <summary>Sets the read boundary and the read allowlist.</summary>
    /// <param name="path">The absolute path of an Autonogrammer <c>sandbox.json</c> (inside a <c>.autonogrammer</c> folder).</param>
    /// <param name="boundary">The boundary to write.</param>
    /// <param name="additionalReadPaths">The read allowlist. Empty is written as <c>[]</c> when the key exists and left out when it does not.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static SafetyEditResult Set(string path, SandboxReadBoundary boundary, IReadOnlyList<string> additionalReadPaths)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(additionalReadPaths);
        return AutonogrammerJsonFile.Update(path, root =>
        {
            root["readBoundary"] = boundary == SandboxReadBoundary.Workspace ? "workspace" : "system";
            if (additionalReadPaths.Count == 0 && !root.ContainsKey("additionalReadPaths"))
            {
                return;
            }

            var array = new JsonArray();
            foreach (var entry in additionalReadPaths)
            {
                array.Add((JsonNode?)JsonValue.Create(entry));
            }

            root["additionalReadPaths"] = array;
        });
    }
}
