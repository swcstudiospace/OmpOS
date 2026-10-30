using System.Text.Json;
using System.Text.Json.Nodes;

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>
/// Read-modify-write of an Autonogrammer-owned JSON file. It refuses any path that is not <c>permissions.json</c>, <c>sandbox.json</c> or <c>run-mode.json</c> directly inside a <c>.autonogrammer</c> folder, so Autonogrammer never writes a Cursor file or any other file.
/// It refuses a file that is not a JSON object, or that holds comments or trailing commas (it would lose them), and tells the user to edit it by hand. The write goes to a temporary file in the same folder and replaces the file in one step.
/// </summary>
internal static class AutonogrammerJsonFile
{
    private static readonly HashSet<string> OwnedNames = new(StringComparer.Ordinal)
    {
        SafetyFileLocations.PermissionsFileName, SafetyFileLocations.SandboxFileName, RunModeStoreFile.FileName,
    };

    /// <summary>Returns whether a path is a file Autonogrammer owns and may write.</summary>
    /// <param name="path">The path.</param>
    /// <returns>True for an owned file.</returns>
    public static bool IsOwned(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return false;
        }

        var folder = Path.GetFileName(Path.GetDirectoryName(path));
        return string.Equals(folder, SafetyFileLocations.AutonogrammerFolder, StringComparison.Ordinal) && OwnedNames.Contains(Path.GetFileName(path));
    }

    /// <summary>Changes the root object of an owned file and writes it back, or does nothing when the change leaves the object as it was.</summary>
    /// <param name="path">The absolute path of the file.</param>
    /// <param name="change">Changes the root object.</param>
    /// <returns>The outcome.</returns>
    public static SafetyEditResult Update(string path, Action<JsonObject> change)
    {
        if (!IsOwned(path))
        {
            return SafetyEditResult.Failure("Autonogrammer writes only permissions.json, sandbox.json and run-mode.json inside a .autonogrammer folder.");
        }

        JsonObject root;
        string? before = null;
        try
        {
            if (Directory.Exists(path))
            {
                return SafetyEditResult.Failure($"{path} is a folder.");
            }

            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                var parsed = TryParse(text, out var problem);
                if (parsed is null)
                {
                    return SafetyEditResult.Failure($"{path} {problem}");
                }

                root = parsed;
                before = Render(root);
            }
            else
            {
                root = new JsonObject();
            }

            change(root);
            var after = Render(root);
            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                return SafetyEditResult.Success;
            }

            Write(path, after);
            return SafetyEditResult.Success;
        }
        catch (IOException exception)
        {
            return SafetyEditResult.Failure($"{path} could not be written: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return SafetyEditResult.Failure($"{path} could not be written: {exception.Message}");
        }
    }

    private static JsonObject? TryParse(string text, out string problem)
    {
        problem = string.Empty;
        try
        {
            if (JsonNode.Parse(text) is JsonObject strict)
            {
                return strict;
            }

            problem = "does not hold one JSON object. Fix or remove it, then try again.";
            return null;
        }
        catch (JsonException)
        {
            var lenient = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            try
            {
                using var document = JsonDocument.Parse(text, lenient);
                problem = "has comments or trailing commas, and rewriting it would lose them. Edit it by hand.";
            }
            catch (JsonException)
            {
                problem = "is not valid JSON. Fix or remove it, then try again.";
            }

            return null;
        }
    }

    private static string Render(JsonObject root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            root.WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void Write(string path, string content)
    {
        var folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, Path.GetFileName(path) + ".tmp-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(temporary, content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
