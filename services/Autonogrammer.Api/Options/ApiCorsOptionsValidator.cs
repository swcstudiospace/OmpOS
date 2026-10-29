using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Options;

/// <summary>
/// Validates <c>Api:Cors:Origins</c> at start. A browser sends the origin as <c>scheme://host[:port]</c> with no path, no trailing slash and no default
/// port, and the CORS middleware compares that text, so an entry written any other way silently never matches and the web head fails with an opaque
/// network error. <c>*</c> would allow every site and is refused: list the web head's origin. Plain <c>http</c> is allowed only for a loopback host.
/// </summary>
internal sealed class ApiCorsOptionsValidator : IValidateOptions<ApiOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string>? failures = null;
        for (var index = 0; index < options.Cors.Origins.Count; index++)
        {
            var problem = Check(options.Cors.Origins[index]);
            if (problem is not null)
            {
                failures ??= [];
                failures.Add($"Api:Cors:Origins:{index.ToString(System.Globalization.CultureInfo.InvariantCulture)} {problem}");
            }
        }

        return failures is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string? Check(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return "is empty.";
        }

        if (string.Equals(origin, "*", StringComparison.Ordinal))
        {
            return "is '*', which allows every site. List the web head's origin, for example https://app.example.com.";
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)))
        {
            return $"('{origin}') must be an http or https origin such as https://app.example.com.";
        }

        if (uri.UserInfo.Length > 0 || !string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            return $"('{origin}') must be exactly scheme://host[:port]: no path, trailing slash, user information or default port, because a browser sends the origin that way and the match is textual.";
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !uri.IsLoopback)
        {
            return $"('{origin}') uses http for a host that is not loopback. Use https.";
        }

        return null;
    }
}
