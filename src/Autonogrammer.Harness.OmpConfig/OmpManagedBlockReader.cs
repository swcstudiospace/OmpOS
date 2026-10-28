using System.Globalization;

namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// Reads a block written by <see cref="OmpManagedBlockWriter"/> back into an <see cref="OmpManagedProvider"/>. It understands only
/// the fixed layout (a provider key, scalar members, a one-entry <c>headers:</c> and a one-entry <c>discovery:</c> map, and a
/// <c>models:</c> list of small maps) and reports anything else as a
/// problem instead of guessing, so a block edited by hand to something richer is never silently reinterpreted.
/// </summary>
internal static class OmpManagedBlockReader
{
    /// <summary>Parses the lines between the two marker comments of a block.</summary>
    /// <param name="id">The provider id named by the markers.</param>
    /// <param name="inner">The lines strictly between the markers.</param>
    /// <param name="provider">The provider when parsing succeeded.</param>
    /// <param name="problem">A plain sentence when parsing failed.</param>
    /// <returns>True when the block was read and the provider passes validation.</returns>
    public static bool TryParse(string id, List<string> inner, out OmpManagedProvider? provider, out string problem)
    {
        provider = null;
        problem = string.Empty;
        var state = new ParseState();
        var sawKey = false;
        foreach (var raw in inner)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string? failure;
            if (!sawKey)
            {
                failure = CheckProviderKey(id, line);
                sawKey = true;
            }
            else if (!state.InModels)
            {
                failure = ReadProviderMember(state, raw, line);
            }
            else
            {
                failure = ReadModelLine(state, line);
            }

            if (failure is not null)
            {
                problem = $"The block for {id} could not be read back: {failure}";
                return false;
            }
        }

        state.FlushModel();
        var built = Build(id, state);
        if (built.Failure is not null)
        {
            problem = $"The block for {id} could not be read back: {built.Failure}";
            return false;
        }

        var invalid = OmpManagedProviderValidator.Validate(built.Provider!);
        if (invalid is not null)
        {
            problem = $"The block for {id} could not be read back: {invalid}";
            return false;
        }

        provider = built.Provider;
        return true;
    }

    private static string? CheckProviderKey(string id, string line)
    {
        if (!OmpModelsLayout.TrySplitKey(line, out var key, out var rest) ||
            !string.Equals(key, id, StringComparison.Ordinal) ||
            OmpModelsLayout.StripComment(rest).Trim().Length > 0)
        {
            return "the provider key line is missing or does not match the markers";
        }

        return null;
    }

    private static string? ReadProviderMember(ParseState state, string raw, string line)
    {
        var indent = raw.Length - raw.TrimStart().Length;
        if (state.MemberIndent < 0)
        {
            state.MemberIndent = indent;
        }

        if (state.Section != ProviderSection.None)
        {
            if (indent > state.MemberIndent)
            {
                return ReadSectionLine(state, line);
            }

            state.Section = ProviderSection.None;
        }

        if (string.Equals(OmpModelsLayout.StripComment(line).Trim(), "models:", StringComparison.Ordinal))
        {
            state.InModels = true;
            return null;
        }

        if (!OmpModelsLayout.TrySplitKey(line, out var key, out var rest) || !TryValue(rest, out var value))
        {
            return "a line is not a simple 'key: value' member";
        }

        switch (key)
        {
            case "baseUrl":
                state.BaseUrl = value;
                return null;
            case "api":
                state.Api = value;
                return null;
            case "apiKey":
                state.ApiKey = value;
                return null;
            case "auth":
                state.Auth = value;
                return null;
            case "authHeader":
                if (!TryBool(value, out var authHeader))
                {
                    return "authHeader is not true or false";
                }

                state.BearerAuthHeader = authHeader;
                return null;
            case "headers" when value.Length == 0:
                state.Section = ProviderSection.Headers;
                return null;
            case "discovery" when value.Length == 0:
                state.Section = ProviderSection.Discovery;
                return null;
            default:
                return $"the member '{key}' is not one Autonogrammer writes";
        }
    }

    private static string? ReadSectionLine(ParseState state, string line)
    {
        if (!OmpModelsLayout.TrySplitKey(line, out var key, out var rest) || !TryValue(rest, out var value))
        {
            return "a line is not a simple 'key: value' member";
        }

        if (state.Section == ProviderSection.Discovery)
        {
            if (!string.Equals(key, "type", StringComparison.Ordinal))
            {
                return $"the discovery member '{key}' is not one Autonogrammer writes";
            }

            state.DiscoveryType = value;
            return null;
        }

        if (state.HeaderName is not null)
        {
            return "more than one header is not something Autonogrammer writes";
        }

        state.HeaderName = key;
        state.HeaderValue = value;
        return null;
    }

    private static string? ReadModelLine(ParseState state, string line)
    {
        var member = line;
        if (line.StartsWith("- ", StringComparison.Ordinal))
        {
            state.FlushModel();
            state.Model = new ModelState();
            member = line[2..].TrimStart();
        }

        if (state.Model is null)
        {
            return "a model member appears before the first '- id:' line";
        }

        if (!OmpModelsLayout.TrySplitKey(member, out var key, out var rest) || !TryValue(rest, out var value))
        {
            return "a model line is not a simple 'key: value' member";
        }

        return ReadModelMember(state.Model, key, rest, value);
    }

    private static string? ReadModelMember(ModelState model, string key, string rest, string value)
    {
        switch (key)
        {
            case "id":
                model.Id = value;
                return null;
            case "name":
                model.Name = value;
                return null;
            case "reasoning":
                if (!TryBool(value, out var reasoning))
                {
                    return "reasoning is not true or false";
                }

                model.Reasoning = reasoning;
                return null;
            case "input":
                if (!TryInput(rest, out var images))
                {
                    return "input is not a list such as [text, image]";
                }

                model.AcceptsImages = images;
                return null;
            case "contextWindow":
                if (!TryNumber(value, out var window))
                {
                    return "contextWindow is not a number";
                }

                model.ContextWindow = window;
                return null;
            case "maxTokens":
                if (!TryNumber(value, out var maxTokens))
                {
                    return "maxTokens is not a number";
                }

                model.MaxTokens = maxTokens;
                return null;
            default:
                return $"the model member '{key}' is not one Autonogrammer writes";
        }
    }

    private static bool TryValue(string rest, out string value)
    {
        var text = rest.Trim();
        return OmpYamlScalar.TryUnquote(text.StartsWith('"') ? text : OmpModelsLayout.StripComment(text), out value);
    }

    private static bool TryBool(string value, out bool result)
    {
        result = string.Equals(value, "true", StringComparison.Ordinal);
        return result || string.Equals(value, "false", StringComparison.Ordinal);
    }

    private static bool TryNumber(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);

    private static bool TryInput(string rest, out bool acceptsImages)
    {
        acceptsImages = false;
        var text = OmpModelsLayout.StripComment(rest).Trim();
        if (text.Length < 2 || text[0] != '[' || text[^1] != ']')
        {
            return false;
        }

        foreach (var item in text[1..^1].Split(','))
        {
            if (string.Equals(item.Trim(), "image", StringComparison.Ordinal))
            {
                acceptsImages = true;
            }
        }

        return true;
    }

    private static (OmpManagedProvider? Provider, string? Failure) Build(string id, ParseState state)
    {
        if (state.BaseUrl is null || state.Api is null)
        {
            return (null, "baseUrl or api is missing");
        }

        if (!OmpProviderApiNames.TryParse(state.Api, out var api))
        {
            return (null, $"the api '{state.Api}' is not supported");
        }

        if (!Uri.TryCreate(state.BaseUrl, UriKind.Absolute, out var baseUrl))
        {
            return (null, "baseUrl is not an absolute URL");
        }
        var noAuthentication = string.Equals(state.Auth, "none", StringComparison.Ordinal);
        if (state.Auth is not null && !noAuthentication && !string.Equals(state.Auth, "apiKey", StringComparison.Ordinal))
        {
            return (null, $"the auth mode '{state.Auth}' is not supported");
        }

        if (state.HeaderName is not null && (noAuthentication || !string.Equals(state.HeaderValue, state.ApiKey, StringComparison.Ordinal)))
        {
            return (null, "the header value is not the apiKey setting");
        }

        var models = new List<OmpManagedModel>(state.Models.Count);
        foreach (var model in state.Models)
        {
            if (model.Id is null)
            {
                return (null, "a model has no id");
            }

            models.Add(new OmpManagedModel(model.Id, model.Name, model.Reasoning, model.AcceptsImages, model.ContextWindow, model.MaxTokens));
        }

        var discovery = state.DiscoveryType is null ? null : new OmpManagedDiscovery(state.DiscoveryType);
        return (
            new OmpManagedProvider(id, baseUrl, api, noAuthentication ? null : state.ApiKey, noAuthentication, models, state.BearerAuthHeader, state.HeaderName, discovery),
            null);
    }

    private sealed class ParseState
    {
        public bool InModels { get; set; }

        public string? BaseUrl { get; set; }

        public string? Api { get; set; }

        public string? ApiKey { get; set; }

        public string? Auth { get; set; }

        public bool BearerAuthHeader { get; set; }

        public int MemberIndent { get; set; } = -1;

        public ProviderSection Section { get; set; }

        public string? HeaderName { get; set; }

        public string? HeaderValue { get; set; }

        public string? DiscoveryType { get; set; }

        public ModelState? Model { get; set; }

        public List<ModelState> Models { get; } = [];

        public void FlushModel()
        {
            if (Model is not null)
            {
                Models.Add(Model);
                Model = null;
            }
        }
    }

    private enum ProviderSection
    {
        None = 0,
        Headers,
        Discovery,
    }

    private sealed class ModelState
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public bool Reasoning { get; set; }

        public bool AcceptsImages { get; set; }

        public int? ContextWindow { get; set; }

        public int? MaxTokens { get; set; }
    }
}
