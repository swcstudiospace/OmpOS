using System.Buffers.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Accounts;

namespace Autonogrammer.Accounts;

/// <summary>
/// The validated connection settings of the hosted auth service: the https project URL and the publishable key, plus what the operator configured
/// about the sign-in methods. Build it with <see cref="Create(string?, string?)"/> or <see cref="Create(AuthConfigResponse)"/>; a result names the
/// offending field and never its value.
/// </summary>
public sealed record GoTrueOptions
{
    /// <summary>The path of the auth service below the project URL; it is also the end of the token issuer.</summary>
    public const string AuthPath = "/auth/v1";

    /// <summary>The default lifetime of an email code, matching the hosted project.</summary>
    public static readonly TimeSpan DefaultEmailCodeExpiry = TimeSpan.FromSeconds(3600);

    /// <summary>The default wait before another code may be requested.</summary>
    public static readonly TimeSpan DefaultResendCooldown = TimeSpan.FromSeconds(60);

    private const string SecretKeyPrefix = "sb_secret_";

    private GoTrueOptions(Uri projectUrl, string publishableKey)
    {
        ProjectUrl = projectUrl;
        PublishableKey = publishableKey;
        Issuer = projectUrl.GetLeftPart(UriPartial.Authority) + projectUrl.AbsolutePath.TrimEnd('/') + AuthPath;
        AuthBaseUri = new Uri(Issuer + "/", UriKind.Absolute);
    }

    /// <summary>Gets the https project URL, for example <c>https://example.supabase.co</c>.</summary>
    public Uri ProjectUrl { get; }

    /// <summary>Gets the publishable key sent as the <c>apikey</c> header. It is public by design and is never sent as a bearer.</summary>
    public string PublishableKey { get; }

    /// <summary>Gets the token issuer of the project: the project URL followed by <see cref="AuthPath"/>. A stored session records it.</summary>
    public string Issuer { get; }

    /// <summary>Gets the base address of the auth service, with a trailing slash.</summary>
    public Uri AuthBaseUri { get; }

    /// <summary>Gets a value indicating whether the PKCE magic link flow is offered (needs owner action O1). False means the code only.</summary>
    public bool MagicLinkEnabled { get; private init; }

    /// <summary>Gets a value indicating whether a one-time code sent by email can be entered.</summary>
    public bool EmailCodeEnabled { get; private init; } = true;

    /// <summary>Gets how long an email code stays valid.</summary>
    public TimeSpan EmailCodeExpiry { get; private init; } = DefaultEmailCodeExpiry;

    /// <summary>Gets how long a person waits before another code may be requested.</summary>
    public TimeSpan ResendCooldown { get; private init; } = DefaultResendCooldown;

    /// <summary>Gets a value indicating whether the first-time signup email already shows a code (owner action O4).</summary>
    public bool SignupEmailHasCode { get; private init; }

    /// <summary>Validates and builds options from the two values every client needs.</summary>
    /// <param name="projectAddress">The project address, for example <c>https://example.supabase.co</c>. It must be absolute https without user information, query or fragment.</param>
    /// <param name="publishableKey">The publishable key. A key starting <c>sb_secret_</c> or a JWT whose <c>role</c> is <c>service_role</c> is refused.</param>
    /// <returns>The options, or the name of the field that failed and a fixed reason. The reason never contains a value.</returns>
    public static GoTrueOptionsResult Create(string? projectAddress, string? publishableKey)
    {
        return CreateCore(projectAddress, publishableKey, magicLink: false, emailCode: true, DefaultEmailCodeExpiry, DefaultResendCooldown, signupEmailHasCode: false);
    }

    /// <summary>Validates and builds options from the answer of <c>GET /auth/config</c>.</summary>
    /// <param name="config">The configuration the API served.</param>
    /// <returns>The options, or the name of the field that failed and a fixed reason. The reason never contains a value.</returns>
    public static GoTrueOptionsResult Create(AuthConfigResponse config)
    {
        ArgumentNullException.ThrowIfNull(config);

        TimeSpan expiry = DefaultEmailCodeExpiry;
        TimeSpan cooldown = DefaultResendCooldown;
        if (config.EmailCode is { } timing)
        {
            if (timing.ExpirySeconds <= 0)
            {
                return GoTrueOptionsResult.Invalid("EmailCode.ExpirySeconds", "must be greater than zero.");
            }

            if (timing.ResendCooldownSeconds < 0)
            {
                return GoTrueOptionsResult.Invalid("EmailCode.ResendCooldownSeconds", "must not be negative.");
            }

            expiry = TimeSpan.FromSeconds(timing.ExpirySeconds);
            cooldown = TimeSpan.FromSeconds(timing.ResendCooldownSeconds);
        }

        bool magicLink = config.Methods?.MagicLink ?? false;
        bool emailCode = config.Methods?.EmailCode ?? true;
        if (magicLink && !emailCode)
        {
            return GoTrueOptionsResult.Invalid("Methods.MagicLink", "needs Methods.EmailCode.");
        }

        return CreateCore(config.ProjectUrl?.OriginalString, config.PublishableKey, magicLink, emailCode, expiry, cooldown, config.SignupEmailHasCode ?? false);
    }

    private static GoTrueOptionsResult CreateCore(
        string? projectUrl,
        string? publishableKey,
        bool magicLink,
        bool emailCode,
        TimeSpan expiry,
        TimeSpan cooldown,
        bool signupEmailHasCode)
    {
        if (string.IsNullOrWhiteSpace(projectUrl))
        {
            return GoTrueOptionsResult.Invalid("ProjectUrl", "is required.");
        }

        if (!Uri.TryCreate(projectUrl, UriKind.Absolute, out Uri? url)
            || !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || url.UserInfo.Length > 0
            || url.Query.Length > 0
            || url.Fragment.Length > 0)
        {
            return GoTrueOptionsResult.Invalid("ProjectUrl", "must be an absolute https URL without user information, query or fragment.");
        }

        if (string.IsNullOrWhiteSpace(publishableKey))
        {
            return GoTrueOptionsResult.Invalid("PublishableKey", "is required.");
        }

        string key = publishableKey.Trim();
        if (key.StartsWith(SecretKeyPrefix, StringComparison.OrdinalIgnoreCase) || IsServiceRoleJwt(key))
        {
            return GoTrueOptionsResult.Invalid("PublishableKey", "must be a publishable key, not a secret or service-role key.");
        }

        return GoTrueOptionsResult.Valid(new GoTrueOptions(url, key)
        {
            MagicLinkEnabled = magicLink,
            EmailCodeEnabled = emailCode,
            EmailCodeExpiry = expiry,
            ResendCooldown = cooldown,
            SignupEmailHasCode = signupEmailHasCode,
        });
    }

    private static bool IsServiceRoleJwt(string key)
    {
        string[] parts = key.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        try
        {
            byte[] payload = Base64Url.DecodeFromChars(parts[1].TrimEnd('='));
            using JsonDocument document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("role", out JsonElement role)
                && role.ValueKind == JsonValueKind.String
                && string.Equals(role.GetString(), "service_role", StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
