using System.Globalization;
using System.Text.Json;

namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>
/// Reads and validates one server entry of a configuration file. Every problem becomes a <see cref="McpDiagnostic"/> that names the server and
/// the field and never carries a value; an entry that cannot be used yields <see langword="null"/> and the reader goes on with the next.
/// </summary>
internal sealed class McpServerReader
{
    private readonly McpLocation _location;
    private readonly McpInterpolationContext _interpolation;
    private readonly List<McpDiagnostic> _diagnostics;
    private readonly string _name;
    private readonly List<string> _unresolved = [];
    private bool _usedEnvironment;

    /// <summary>Creates a reader for one entry.</summary>
    /// <param name="location">The file the entry is in.</param>
    /// <param name="interpolation">The values variable references expand to.</param>
    /// <param name="diagnostics">Receives every problem.</param>
    /// <param name="name">The entry's key.</param>
    public McpServerReader(McpLocation location, McpInterpolationContext interpolation, List<McpDiagnostic> diagnostics, string name)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(interpolation);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(name);
        _location = location;
        _interpolation = interpolation;
        _diagnostics = diagnostics;
        _name = name;
    }

    /// <summary>Reads the entry.</summary>
    /// <param name="entry">The entry's value.</param>
    /// <returns>The server, or <see langword="null"/> when the entry is unusable (a diagnostic says why).</returns>
    public McpServerSpec? Read(JsonElement entry)
    {
        if (!McpServerNames.IsValid(_name))
        {
            Report(McpDiagnosticSeverity.Error, "The name must be 1 to 64 letters, digits, '_', '-' or '.'. Rename the entry; it is ignored.");
            return null;
        }

        if (entry.ValueKind != JsonValueKind.Object)
        {
            Report(McpDiagnosticSeverity.Error, "The entry is not an object and is ignored.");
            return null;
        }

        var transport = ResolveTransport(entry);
        if (transport is null)
        {
            return null;
        }

        return transport == McpTransportKind.Stdio ? ReadStdio(entry) : ReadRemote(entry, transport.Value);
    }

    private McpTransportKind? ParseTypeField(JsonElement type)
    {
        if (type.ValueKind != JsonValueKind.String)
        {
            Report(McpDiagnosticSeverity.Error, "The 'type' field is not a string; the entry is ignored.");
            return null;
        }

        McpTransportKind? transport = (type.GetString() ?? string.Empty).ToUpperInvariant() switch
        {
            "STDIO" => McpTransportKind.Stdio,
            "HTTP" or "STREAMABLE-HTTP" or "STREAMABLEHTTP" or "STREAMABLE_HTTP" => McpTransportKind.Http,
            "SSE" => McpTransportKind.Sse,
            _ => null,
        };
        if (transport is null)
        {
            Report(McpDiagnosticSeverity.Error, "The 'type' field is not one of stdio, http or sse; the entry is ignored.");
        }

        return transport;
    }

    private McpTransportKind? ResolveTransport(JsonElement entry)
    {
        var hasCommand = entry.TryGetProperty("command", out var command) && command.ValueKind != JsonValueKind.Null;
        var hasUrl = entry.TryGetProperty("url", out var url) && url.ValueKind != JsonValueKind.Null;

        McpTransportKind? transport;
        if (entry.TryGetProperty("type", out var type) && type.ValueKind != JsonValueKind.Null)
        {
            transport = ParseTypeField(type);
            if (transport is null)
            {
                return null;
            }
        }
        else if (hasCommand && hasUrl)
        {
            Report(McpDiagnosticSeverity.Error, "The entry has both 'command' and 'url'. Add a 'type' of stdio, http or sse; the entry is ignored.");
            return null;
        }
        else if (hasCommand)
        {
            transport = McpTransportKind.Stdio;
        }
        else if (hasUrl)
        {
            transport = McpTransportKind.Http;
        }
        else
        {
            Report(McpDiagnosticSeverity.Error, "The entry has neither 'command' nor 'url'; it is ignored.");
            return null;
        }

        if (transport == McpTransportKind.Stdio && !hasCommand)
        {
            Report(McpDiagnosticSeverity.Error, "A stdio server needs a 'command'; the entry is ignored.");
            return null;
        }

        if (transport != McpTransportKind.Stdio && !hasUrl)
        {
            Report(McpDiagnosticSeverity.Error, "A remote server needs a 'url'; the entry is ignored.");
            return null;
        }

        var stray = transport == McpTransportKind.Stdio ? hasUrl : hasCommand;
        if (stray)
        {
            Report(McpDiagnosticSeverity.Warning, transport == McpTransportKind.Stdio
                ? "The 'url' field of a stdio server is ignored."
                : "The 'command' field of a remote server is ignored.");
        }

        return transport;
    }

    private McpServerSpec? ReadStdio(JsonElement entry)
    {
        if (!TryReadString(entry.GetProperty("command"), "command", McpConfigLimits.MaxCommandLength, out var rawCommand))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawCommand))
        {
            Report(McpDiagnosticSeverity.Error, "The 'command' field must be a non-empty string; the entry is ignored.");
            return null;
        }

        if (!TryReadArgs(entry, out var rawArgs) || !TryReadPairs(entry, "env", "env", out var rawEnv))
        {
            return null;
        }

        var command = Expand("command", rawCommand);
        var args = new List<string>(rawArgs.Count);
        for (var index = 0; index < rawArgs.Count; index++)
        {
            args.Add(Expand(string.Create(CultureInfo.InvariantCulture, $"args[{index}]"), rawArgs[index]));
        }

        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in rawEnv)
        {
            env[pair.Key] = Expand("env." + pair.Key, pair.Value);
        }

        NoteIgnoredFields(entry, stdio: true);
        var preview = Truncate(rawArgs.Count == 0 ? rawCommand : rawCommand + " " + string.Join(' ', rawArgs));
        return Build(McpTransportKind.Stdio, entry, preview, command, args, env, null, new Dictionary<string, string>(StringComparer.Ordinal), rawCommand, rawArgs, rawEnv, null, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private McpServerSpec? ReadRemote(JsonElement entry, McpTransportKind transport)
    {
        if (!TryReadString(entry.GetProperty("url"), "url", McpConfigLimits.MaxValueLength, out var rawUrl))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            Report(McpDiagnosticSeverity.Error, "The 'url' field must be a non-empty string; the entry is ignored.");
            return null;
        }

        if (!TryReadPairs(entry, "headers", "headers", out var rawHeaders))
        {
            return null;
        }

        foreach (var header in rawHeaders.Keys)
        {
            if (!IsHeaderName(header))
            {
                Report(McpDiagnosticSeverity.Error, "The header name '" + header + "' is not a valid HTTP header name; the entry is ignored.");
                return null;
            }
        }

        var expandedUrl = Expand("url", rawUrl);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in rawHeaders)
        {
            headers[pair.Key] = Expand("headers." + pair.Key, pair.Value);
        }

        Uri? url = null;
        if (_unresolved.Count == 0 || !expandedUrl.Contains("${", StringComparison.Ordinal))
        {
            if (Uri.TryCreate(expandedUrl, UriKind.Absolute, out var parsed)
                && (string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
            {
                url = parsed;
            }
            else
            {
                Report(McpDiagnosticSeverity.Error, "The 'url' field is not an absolute http or https URL; the entry is ignored.");
                return null;
            }
        }

        NoteIgnoredFields(entry, stdio: false);
        var preview = url is null ? "(URL has unresolved variables)" : url.GetLeftPart(UriPartial.Authority);
        return Build(transport, entry, preview, null, [], new Dictionary<string, string>(StringComparer.Ordinal), url, headers, null, [], new Dictionary<string, string>(StringComparer.Ordinal), rawUrl, rawHeaders);
    }

    private McpServerSpec Build(
        McpTransportKind transport,
        JsonElement entry,
        string preview,
        string? command,
        List<string> args,
        Dictionary<string, string> env,
        Uri? url,
        Dictionary<string, string> headers,
        string? rawCommand,
        List<string> rawArgs,
        Dictionary<string, string> rawEnv,
        string? rawUrl,
        Dictionary<string, string> rawHeaders)
    {
        var enabled = true;
        if (entry.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True)
        {
            enabled = false;
        }

        if (entry.TryGetProperty("enabled", out var flag) && flag.ValueKind == JsonValueKind.False)
        {
            enabled = false;
        }

        var hasOAuth = (entry.TryGetProperty("auth", out var auth) && auth.ValueKind == JsonValueKind.Object)
            || (entry.TryGetProperty("oauth", out var oauth) && oauth.ValueKind == JsonValueKind.Object);
        if (hasOAuth)
        {
            Report(McpDiagnosticSeverity.Info, "The OAuth settings are not passed to omp. If the server asks you to sign in, run /mcp reauth " + _name + " in the session.");
        }

        return new McpServerSpec
        {
            Name = _name,
            Transport = transport,
            Scope = _location.Scope,
            Format = _location.Format,
            Origin = _location.Origin,
            Priority = _location.Priority,
            Command = command,
            Args = args,
            Env = env,
            Url = url,
            Headers = headers,
            EnabledInFile = enabled,
            HasOAuthSettings = hasOAuth,
            UsesEnvironmentVariables = _usedEnvironment,
            UnresolvedVariables = [.. _unresolved.Distinct(StringComparer.Ordinal)],
            CommandPreview = preview,
            ConsentIdentity = McpConsentIdentity.Compute(transport, rawCommand, rawArgs, rawEnv, rawUrl, rawHeaders),
        };
    }

    private void NoteIgnoredFields(JsonElement entry, bool stdio)
    {
        if (stdio && entry.TryGetProperty("envFile", out _))
        {
            Report(McpDiagnosticSeverity.Warning, "The 'envFile' field is not read: omp receives the 'env' entries only. Move the variables into 'env'.");
        }

        if (stdio && entry.TryGetProperty("cwd", out _))
        {
            Report(McpDiagnosticSeverity.Warning, "The 'cwd' field is not passed: the ACP entry of a stdio server has no working directory.");
        }
    }

    private bool TryReadArgs(JsonElement entry, out List<string> args)
    {
        args = [];
        if (!entry.TryGetProperty("args", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            Report(McpDiagnosticSeverity.Error, "The 'args' field is not an array; the entry is ignored.");
            return false;
        }

        if (element.GetArrayLength() > McpConfigLimits.MaxArgs)
        {
            Report(McpDiagnosticSeverity.Error, "The 'args' field has more than " + McpConfigLimits.MaxArgs.ToString(CultureInfo.InvariantCulture) + " entries; the entry is ignored.");
            return false;
        }

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            var label = string.Create(CultureInfo.InvariantCulture, $"args[{index}]");
            if (!TryReadScalar(item, label, out var text))
            {
                return false;
            }

            args.Add(text);
            index++;
        }

        return true;
    }

    private bool TryReadPairs(JsonElement entry, string property, string label, out Dictionary<string, string> pairs)
    {
        pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!entry.TryGetProperty(property, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            Report(McpDiagnosticSeverity.Error, "The '" + label + "' field is not an object; the entry is ignored.");
            return false;
        }

        foreach (var member in element.EnumerateObject())
        {
            if (pairs.Count >= McpConfigLimits.MaxPairs)
            {
                Report(McpDiagnosticSeverity.Error, "The '" + label + "' field has more than " + McpConfigLimits.MaxPairs.ToString(CultureInfo.InvariantCulture) + " entries; the entry is ignored.");
                return false;
            }

            if (string.Equals(property, "env", StringComparison.Ordinal) && !McpVariableInterpolator.IsVariableName(member.Name))
            {
                Report(McpDiagnosticSeverity.Error, "The environment variable name '" + member.Name + "' is not valid; the entry is ignored.");
                return false;
            }

            if (!TryReadScalar(member.Value, label + "." + member.Name, out var text))
            {
                return false;
            }

            pairs[member.Name] = text;
        }

        return true;
    }

    private bool TryReadScalar(JsonElement element, string label, out string text)
    {
        text = string.Empty;
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return TryReadString(element, label, McpConfigLimits.MaxValueLength, out text);
            case JsonValueKind.Number:
                text = element.GetRawText();
                return true;
            case JsonValueKind.True:
                text = "true";
                return true;
            case JsonValueKind.False:
                text = "false";
                return true;
            default:
                Report(McpDiagnosticSeverity.Error, "The '" + label + "' field is not a string, number or boolean; the entry is ignored.");
                return false;
        }
    }

    private bool TryReadString(JsonElement element, string label, int maxLength, out string text)
    {
        text = string.Empty;
        if (element.ValueKind != JsonValueKind.String)
        {
            Report(McpDiagnosticSeverity.Error, "The '" + label + "' field is not a string; the entry is ignored.");
            return false;
        }

        var value = element.GetString() ?? string.Empty;
        if (value.Length > maxLength)
        {
            Report(McpDiagnosticSeverity.Error, "The '" + label + "' field is longer than " + maxLength.ToString(CultureInfo.InvariantCulture) + " characters; the entry is ignored.");
            return false;
        }

        text = value;
        return true;
    }

    private string Expand(string field, string raw)
    {
        var result = McpVariableInterpolator.Expand(raw, _interpolation);
        _usedEnvironment |= result.UsedEnvironment;
        foreach (var name in result.Unresolved)
        {
            _unresolved.Add(name);
            Report(McpDiagnosticSeverity.Error, DescribeUnresolved(name, field));
        }

        foreach (var name in result.Unknown)
        {
            Report(McpDiagnosticSeverity.Warning, "'${" + name + "}' in '" + field + "' is not a variable of this file format and is passed on unchanged.");
        }

        return result.Value;
    }

    private static string DescribeUnresolved(string name, string field)
    {
        if (name.StartsWith("input:", StringComparison.Ordinal))
        {
            return "'${" + name + "}' in '" + field + "' is not supported: Autonogrammer has no input prompts. Use ${env:NAME} instead. The server is not offered.";
        }

        if (name.StartsWith("env:", StringComparison.Ordinal))
        {
            return "The environment variable " + name[4..] + " used in '" + field + "' is not set. The server is not offered until it is.";
        }

        if (McpVariableInterpolator.IsVariableName(name))
        {
            return "The environment variable " + name + " used in '" + field + "' is not set and has no default. The server is not offered until it is.";
        }

        return "'${" + name + "}' in '" + field + "' has no value here. The server is not offered.";
    }

    private static bool IsHeaderName(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character, StringComparison.Ordinal)))
            {
                return false;
            }
        }

        return true;
    }

    private static string Truncate(string text) =>
        text.Length <= McpConfigLimits.MaxPreviewLength ? text : string.Concat(text.AsSpan(0, McpConfigLimits.MaxPreviewLength), "...");

    private void Report(McpDiagnosticSeverity severity, string message) =>
        _diagnostics.Add(new McpDiagnostic(severity, _location.Origin, _name, message));
}
