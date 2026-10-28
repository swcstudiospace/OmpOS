using System.Globalization;

namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// Renders a managed provider in the one fixed layout the reader understands. Every string is double-quoted so ids such as
/// <c>123</c> or <c>llama3:8b</c> stay strings. The block is delimited by two comment lines and sits as a child of the root
/// <c>providers:</c> mapping.
/// </summary>
internal static class OmpManagedBlockWriter
{
    /// <summary>Renders the block lines, without line endings.</summary>
    /// <param name="provider">A provider that passed <see cref="OmpManagedProviderValidator.Validate"/>.</param>
    /// <param name="childIndent">The indentation of the children of <c>providers:</c>.</param>
    /// <param name="step">The indentation one nesting level adds.</param>
    /// <returns>The lines, markers included.</returns>
    public static List<string> Render(OmpManagedProvider provider, int childIndent, int step)
    {
        var lines = new List<string>
        {
            Pad(childIndent) + OmpModelsLayout.BeginMarker + provider.Id,
            Pad(childIndent) + OmpYamlScalar.Quote(provider.Id) + ":",
        };

        var property = Pad(childIndent + step);
        lines.Add(property + "baseUrl: " + OmpYamlScalar.Quote(provider.BaseUrl.OriginalString));
        lines.Add(property + "api: " + OmpProviderApiNames.ToYaml(provider.Api));
        if (provider.NoAuthentication)
        {
            lines.Add(property + "auth: none");
        }
        else
        {
            lines.Add(property + "apiKey: " + OmpYamlScalar.Quote(provider.ApiKeyEnvName!));
            if (provider.Api == OmpProviderApi.AnthropicMessages)
            {
                // omp shapes custom anthropic-messages requests like OAuth when auth is omitted; a key needs the plain shape.
                lines.Add(property + "auth: apiKey");
            }
        }

        if (provider.BearerAuthHeader)
        {
            lines.Add(property + "authHeader: true");
        }

        if (provider.KeyHeaderName is not null)
        {
            lines.Add(property + "headers:");
            lines.Add(Pad(childIndent + (2 * step)) + OmpYamlScalar.Quote(provider.KeyHeaderName) + ": " + OmpYamlScalar.Quote(provider.ApiKeyEnvName!));
        }

        if (provider.Discovery is not null)
        {
            lines.Add(property + "discovery:");
            lines.Add(Pad(childIndent + (2 * step)) + "type: " + OmpYamlScalar.Quote(provider.Discovery.Type));
        }

        if (provider.Models.Count > 0)
        {
            AddModels(lines, provider, childIndent, step);
        }

        lines.Add(Pad(childIndent) + OmpModelsLayout.EndMarker + provider.Id);
        return lines;
    }

    private static void AddModels(List<string> lines, OmpManagedProvider provider, int childIndent, int step)
    {
        lines.Add(Pad(childIndent + step) + "models:");
        var item = Pad(childIndent + (2 * step));
        var itemProperty = item + "  ";
        foreach (var model in provider.Models)
        {
            lines.Add(item + "- id: " + OmpYamlScalar.Quote(model.Id));
            if (model.Name is not null)
            {
                lines.Add(itemProperty + "name: " + OmpYamlScalar.Quote(model.Name));
            }

            lines.Add(itemProperty + "reasoning: " + (model.Reasoning ? "true" : "false"));
            lines.Add(itemProperty + "input: " + (model.AcceptsImages ? "[text, image]" : "[text]"));
            if (model.ContextWindow is { } contextWindow)
            {
                lines.Add(itemProperty + "contextWindow: " + contextWindow.ToString(CultureInfo.InvariantCulture));
            }

            if (model.MaxTokens is { } maxTokens)
            {
                lines.Add(itemProperty + "maxTokens: " + maxTokens.ToString(CultureInfo.InvariantCulture));
            }
        }

    }

    private static string Pad(int width) => new(' ', width);
}
