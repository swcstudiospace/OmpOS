using System.Text.Json;
using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>Reads the JSON that <c>podman ps --format json</c> prints: an array of container objects with <c>Id</c>, <c>Names</c>, <c>Image</c>, <c>State</c> and <c>Labels</c>. Everything else in the objects (environment, mounts, command) is ignored and never kept.</summary>
internal static class PodmanContainerListParser
{
    private const int MaxContainers = 500;
    private const int MaxFieldLength = 512;

    /// <summary>Parses the output.</summary>
    /// <param name="json">The text podman wrote to stdout.</param>
    /// <returns>The containers that have a valid id and name, in order, at most 500.</returns>
    /// <exception cref="JsonException">The text is not a JSON array.</exception>
    internal static IReadOnlyList<ContainerSummary> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The container list is not a JSON array.");
        }

        var containers = new List<ContainerSummary>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (containers.Count >= MaxContainers || element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = StringOf(element, "Id");
            var name = FirstName(element);
            if (id is null || name is null || !ContainerProfileRules.IsValidContainerReference(name))
            {
                continue;
            }

            containers.Add(new ContainerSummary(id, name, StringOf(element, "Image") ?? string.Empty, StringOf(element, "State") ?? string.Empty, DevContainerFolder(element)));
        }

        return containers;
    }

    private static string? StringOf(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? Limit(value.GetString()) : null;

    private static string? Limit(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > MaxFieldLength || value.Any(char.IsControl) ? null : value;

    private static string? FirstName(JsonElement element)
    {
        if (!element.TryGetProperty("Names", out var names))
        {
            return null;
        }

        if (names.ValueKind == JsonValueKind.String)
        {
            return Limit(names.GetString());
        }

        if (names.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var name in names.EnumerateArray())
        {
            if (name.ValueKind == JsonValueKind.String && Limit(name.GetString()) is { } text)
            {
                return text;
            }
        }

        return null;
    }

    private static string? DevContainerFolder(JsonElement element) =>
        element.TryGetProperty("Labels", out var labels)
        && labels.ValueKind == JsonValueKind.Object
        && labels.TryGetProperty(DevContainerResolver.LocalFolderLabel, out var folder)
        && folder.ValueKind == JsonValueKind.String
            ? Limit(folder.GetString())
            : null;
}
