using System.Text.Json.Serialization;

namespace OmpOS.Workspace;

/// <summary>
/// Source-generated JSON metadata for convention files. Unknown members are rejected so that a misspelled key
/// is reported as a malformed file instead of being ignored.
/// </summary>
[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ModeFile))]
[JsonSerializable(typeof(SetupFile))]
internal sealed partial class WorkspaceJsonContext : JsonSerializerContext
{
}
