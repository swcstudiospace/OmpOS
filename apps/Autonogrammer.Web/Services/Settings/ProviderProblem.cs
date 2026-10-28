using System.Text.Json;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>The stable code and the detail of an RFC 9457 problem the API answered with.</summary>
/// <param name="Status">The HTTP status code.</param>
/// <param name="Code">The stable code from the <c>type</c> member (<c>/problems/{code}</c>), or null.</param>
/// <param name="Detail">The <c>detail</c> member, or null.</param>
internal sealed record ProviderProblem(int Status, string? Code, string? Detail)
{
    private const string TypePrefix = "/problems/";
    private const int MaxDetailLength = 300;

    /// <summary>Reads a problem body. A body that is not a problem yields a problem with a status only.</summary>
    /// <param name="status">The HTTP status code.</param>
    /// <param name="body">The response body, or null.</param>
    /// <returns>The problem.</returns>
    public static ProviderProblem Parse(int status, string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new ProviderProblem(status, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ProviderProblem(status, null, null);
            }

            var type = Read(document.RootElement, "type");
            var code = type is not null && type.StartsWith(TypePrefix, StringComparison.Ordinal) ? type[TypePrefix.Length..] : null;
            var detail = Read(document.RootElement, "detail");
            return new ProviderProblem(status, code, detail is { Length: > MaxDetailLength } ? detail[..MaxDetailLength] : detail);
        }
        catch (JsonException)
        {
            return new ProviderProblem(status, null, null);
        }
    }

    private static string? Read(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
