using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autonogrammer.Api.Hosting;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Dispatch.Devices;

/// <summary>Pairing and connect routes. Later plans add the device, command and feed routes to <see cref="MapDispatchEndpoints"/>.</summary>
internal static partial class DeviceEndpoints
{
    /// <summary>Maps pairing and connect. A second call maps nothing.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapDispatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (!endpoints.ServiceProvider.GetRequiredService<DispatchEndpointsMapped>().TryMark())
        {
            return endpoints;
        }

        var routes = endpoints.MapGroup(string.Empty).WithMetadata(DispatchRouteMarker.Instance);
        routes.MapPost("/dispatch/devices", PairAsync)
            .WithTags("Dispatch")
            .WithSummary("Pairs a desktop and returns a device credential.")
            .Accepts<PairingBody>("application/json")
            .Produces<PairingReply>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting(DispatchRateLimits.DispatchPairingRateLimiterPolicy.PolicyName);
        routes.MapGet("/dispatch/devices/{id}/connect", ConnectAsync)
            .WithTags("Dispatch")
            .WithSummary("Opens the device connection. The hub is not registered yet.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(DispatchDeviceScheme.PolicyName)
            .RequireRateLimiting(DispatchRateLimits.DispatchConnectRateLimiterPolicy.PolicyName)
            .WithRequestTimeout(HostingPolicies.NoTimeout);
        return endpoints;
    }

    private static async Task<Results<JsonHttpResult<PairingReply>, ProblemHttpResult>> PairAsync(
        HttpContext http,
        IDispatchStore store,
        DeviceCredentialIssuer issuer,
        IOptions<DispatchOptions> options,
        DispatchMetrics metrics,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var body = await http.Request.ReadFromJsonAsync(DispatchApiJsonContext.Default.PairingBody, cancellationToken).ConfigureAwait(false);
        var user = http.User;
        var accountId = user.FindFirst(JwtBearerSetup.SubjectClaimType)?.Value;
        if (string.IsNullOrEmpty(accountId))
        {
            metrics.Pairing("refused", "unauthenticated");
            return Problem(StatusCodes.Status401Unauthorized, "unauthenticated", "Sign-in required.", "Pairing requires a signed-in account.");
        }

        if (!SignInFreshness.IsFresh(user, time.GetUtcNow(), options.Value.Pairing.MaxSignInAge, out var sessionId))
        {
            metrics.Pairing("refused", "recent_sign_in_required");
            return Problem(StatusCodes.Status403Forbidden, "recent_sign_in_required", "Recent sign-in required.", "Pairing requires a full sign-in no older than the configured age.");
        }

        if (!TryNormalize(body, out var normalized, out var error))
        {
            metrics.Pairing("refused", "invalid_body");
            return Problem(StatusCodes.Status400BadRequest, "invalid_body", "Invalid pairing body.", error);
        }

        var deviceId = Guid.NewGuid().ToString("N");
        var thumbprint = DeviceCredentialIssuer.Thumbprint(normalized.PublicKey.X!, normalized.PublicKey.Y!);
        var issued = issuer.Issue(deviceId, accountId, thumbprint);
        var created = await store.CreateDeviceAsync(
            new CreateDispatchDevice(accountId, deviceId, normalized.Name, normalized.PublicKey.Canonical, issued.Jti, issued.ExpiresAt, normalized.Capabilities, sessionId),
            cancellationToken).ConfigureAwait(false);
        if (created.Status == DispatchDeviceCreateStatus.DeviceLimitReached)
        {
            metrics.Pairing("refused", "device_limit_reached");
            return Problem(StatusCodes.Status409Conflict, "device_limit_reached", "Device limit reached.", "This account already has the maximum number of devices.");
        }

        if (created.Status != DispatchDeviceCreateStatus.Created)
        {
            metrics.Pairing("refused", "device_id_taken");
            return Problem(StatusCodes.Status409Conflict, "device_id_taken", "Device identifier taken.", "That device identifier is already in use.");
        }

        metrics.Pairing("created", "ok");
        return TypedResults.Json(Reply(http, options.Value, issuer, deviceId, issued, normalized.Capabilities), DeviceEndpoints.DispatchApiJsonContext.Default.PairingReply, statusCode: StatusCodes.Status201Created);
    }


    private static PairingReply Reply(HttpContext http, DispatchOptions options, DeviceCredentialIssuer issuer, string deviceId, DeviceCredentialIssuer.IssuedCredential issued, IReadOnlyList<DispatchCapabilitySnapshot> capabilities)
    {
        var hub = issuer.HubKey();
        var limits = options.Limits;
        return new PairingReply
        {
            DeviceId = deviceId,
            Credential = issued.Token,
            CredentialExpiresAt = issued.ExpiresAt.ToUnixTimeSeconds(),
            HubOrigin = string.IsNullOrWhiteSpace(options.HubOrigin) ? $"{http.Request.Scheme}://{http.Request.Host}" : options.HubOrigin,
            VerificationKeys =
            [
                new VerificationKeyReply
                {
                    Kid = hub.Kid,
                    Jwk = hub.Jwk,
                    NotBefore = hub.NotBefore.ToUnixTimeSeconds(),
                    Expires = hub.Expires.ToUnixTimeSeconds(),
                },
            ],
            Limits = new LimitsReply
            {
                MaxQueuedPerDevice = limits.MaxQueuedPerDevice,
                DevicesPerAccount = limits.DevicesPerAccount,
                MaxPayloadBytes = options.MaxPayloadBytes,
                QueueTtlSeconds = (int)options.QueueTtl.TotalSeconds,
            },
            Capabilities = capabilities,
        };
    }

    private static async Task<IResult> ConnectAsync(
        string id,
        HttpContext http,
        IAuthorizationService authorization,
        IDispatchConnectHandler handler,
        DispatchMetrics metrics,
        CancellationToken cancellationToken)
    {
        var accountId = http.User.FindFirst(DeviceCredentialIssuer.OwnerClaim)?.Value;

        if (string.IsNullOrEmpty(accountId))
        {
            metrics.Connect("refused", "unauthenticated");
            return WebProblems.Of(StatusCodes.Status401Unauthorized, "unauthenticated", "Credential required.", "The connect route accepts a device credential.");
        }

        var rejected = await DispatchDeviceScheme.DispatchOwnerCheck.RejectIfNotAllowedAsync(authorization, accountId, cancellationToken).ConfigureAwait(false);
        if (rejected is not null)
        {
            metrics.Connect("refused", "subject_not_allowed");
            return rejected;
        }

        metrics.Connect("not_ready", "hub_not_ready");
        return await handler.HandleAsync(http, id, accountId, cancellationToken).ConfigureAwait(false);
    }

    private static ProblemHttpResult Problem(int status, string code, string title, string detail) =>
        WebProblems.Of(status, code, title, detail);

    private static bool TryNormalize(PairingBody? body, out NormalizedPairing normalized, out string error)
    {
        normalized = default;
        error = "The pairing body is missing.";
        if (body is null)
        {
            return false;
        }

        var name = body.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 64)
        {
            error = "name must be 1 to 64 characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(body.Platform))
        {
            error = "platform is required.";
            return false;
        }

        if (body.PublicKey is not { Kty: "EC", Crv: "P-256" } key || string.IsNullOrWhiteSpace(key.X) || string.IsNullOrWhiteSpace(key.Y))
        {
            error = "publicKey must be an EC P-256 JWK.";
            return false;
        }

        if (body.Capabilities is not { Count: > 0 } capabilities)
        {
            error = "capabilities must list at least one entry.";
            return false;
        }

        foreach (var capability in capabilities)
        {
            if (!Enum.IsDefined(capability.Id) || capability.Version < 1)
            {
                error = "capabilities contain an unknown id or a version below 1.";
                return false;
            }
        }

        normalized = new NormalizedPairing(name, key, capabilities);
        return true;
    }

    private readonly record struct NormalizedPairing(string Name, DevicePublicKey PublicKey, IReadOnlyList<DispatchCapabilitySnapshot> Capabilities);

    /// <summary>Marks that dispatch routes have been mapped.</summary>
    internal sealed class DispatchEndpointsMapped
    {
        private int _mapped;

        /// <summary>Marks the routes mapped.</summary>
        /// <returns><see langword="false"/> when they were already mapped.</returns>
        public bool TryMark() => Interlocked.CompareExchange(ref _mapped, 1, 0) == 0;
    }

    /// <summary>Body of <c>POST /dispatch/devices</c>.</summary>
    internal sealed class PairingBody
    {
        /// <summary>Gets or sets the display name.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the desktop platform.</summary>
        public string? Platform { get; set; }

        /// <summary>Gets or sets the desktop application version.</summary>
        public string? AppVersion { get; set; }

        /// <summary>Gets or sets the relay protocol versions the desktop speaks.</summary>
        public IReadOnlyList<string>? ProtocolVersions { get; set; }

        /// <summary>Gets or sets the capability snapshot.</summary>
        public IReadOnlyList<DispatchCapabilitySnapshot>? Capabilities { get; set; }

        /// <summary>Gets or sets the device public key.</summary>
        public DevicePublicKey? PublicKey { get; set; }
    }

    /// <summary>An EC public JWK.</summary>
    internal sealed class DevicePublicKey
    {
        /// <summary>Gets or sets the key type.</summary>
        public string? Kty { get; set; }

        /// <summary>Gets or sets the curve.</summary>
        public string? Crv { get; set; }

        /// <summary>Gets or sets the X coordinate.</summary>
        public string? X { get; set; }

        /// <summary>Gets or sets the Y coordinate.</summary>
        public string? Y { get; set; }

        /// <summary>Gets the canonical JWK stored on the device row.</summary>
        public string Canonical => $"{{\"crv\":\"{Crv}\",\"kty\":\"{Kty}\",\"x\":\"{X}\",\"y\":\"{Y}\"}}";
    }

    /// <summary>201 body of a successful pairing.</summary>
    internal sealed class PairingReply
    {
        /// <summary>Gets or sets the device identifier.</summary>
        public required string DeviceId { get; set; }

        /// <summary>Gets or sets the device credential. Shown once.</summary>
        public required string Credential { get; set; }

        /// <summary>Gets or sets the credential expiry, Unix seconds.</summary>
        public required long CredentialExpiresAt { get; set; }

        /// <summary>Gets or sets the hub origin.</summary>
        public required string HubOrigin { get; set; }

        /// <summary>Gets or sets the hub verification keys.</summary>
        public required IReadOnlyList<VerificationKeyReply> VerificationKeys { get; set; }

        /// <summary>Gets or sets the limits the device must honour.</summary>
        public required LimitsReply Limits { get; set; }

        /// <summary>Gets or sets the capability snapshot stored on the device.</summary>
        public required IReadOnlyList<DispatchCapabilitySnapshot> Capabilities { get; set; }
    }

    /// <summary>One hub verification key in the pairing reply.</summary>
    internal sealed class VerificationKeyReply
    {
        /// <summary>Gets or sets the key identifier.</summary>
        public required string Kid { get; set; }

        /// <summary>Gets or sets the ES256 public JWK.</summary>
        public required IReadOnlyDictionary<string, string> Jwk { get; set; }

        /// <summary>Gets or sets the start of the validity window, Unix seconds.</summary>
        public required long NotBefore { get; set; }

        /// <summary>Gets or sets the end of the validity window, Unix seconds.</summary>
        public required long Expires { get; set; }
    }

    /// <summary>Limits returned at pairing.</summary>
    internal sealed class LimitsReply
    {
        /// <summary>Gets or sets the queued-command cap.</summary>
        public required int MaxQueuedPerDevice { get; set; }

        /// <summary>Gets or sets the device cap.</summary>
        public required int DevicesPerAccount { get; set; }

        /// <summary>Gets or sets the payload cap.</summary>
        public required int MaxPayloadBytes { get; set; }

        /// <summary>Gets or sets the queue time, in seconds.</summary>
        public required int QueueTtlSeconds { get; set; }
    }

    /// <summary>Decides whether the newest non-refresh <c>amr</c> entry is inside the pairing window. <c>iat</c> is not read.</summary>
    internal static class SignInFreshness
    {
        /// <summary>Returns whether the principal has a full sign-in no older than <paramref name="maxAge"/>.</summary>
        /// <param name="user">The caller.</param>
        /// <param name="now">The current time.</param>
        /// <param name="maxAge">The maximum age.</param>
        /// <param name="sessionId">The <c>session_id</c> claim when the sign-in is fresh.</param>
        /// <returns><see langword="true"/> when pairing may proceed.</returns>
        public static bool IsFresh(ClaimsPrincipal user, DateTimeOffset now, TimeSpan maxAge, out string sessionId)
        {
            ArgumentNullException.ThrowIfNull(user);
            sessionId = string.Empty;
            var newest = DateTimeOffset.MinValue;
            var found = false;
            foreach (var claim in user.FindAll("amr"))
            {
                if (TryRead(claim.Value, out var method, out var timestamp) && !string.Equals(method, "token_refresh", StringComparison.Ordinal) && timestamp > newest)
                {
                    newest = timestamp;
                    found = true;
                }
            }

            var session = user.FindFirst("session_id")?.Value;
            if (!found || string.IsNullOrEmpty(session) || now - newest > maxAge)
            {
                return false;
            }

            sessionId = session;
            return true;
        }

        private static bool TryRead(string value, out string method, out DateTimeOffset timestamp)
        {
            method = string.Empty;
            timestamp = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.StartsWith('{'))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize(value, DeviceEndpoints.DispatchApiJsonContext.Default.AmrEntry);
                    if (entry?.Method is null || entry.Timestamp is not > 0)
                    {
                        return false;
                    }

                    method = entry.Method;
                    timestamp = DateTimeOffset.FromUnixTimeSeconds(entry.Timestamp.Value);
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
            }

            method = value;
            return false;
        }
    }

    /// <summary>One <c>amr</c> entry.</summary>
    internal sealed class AmrEntry
    {
        /// <summary>Gets or sets the method.</summary>
        public string? Method { get; set; }

        /// <summary>Gets or sets the Unix timestamp.</summary>
        public long? Timestamp { get; set; }
    }

    /// <summary>Source-generated JSON metadata for the pairing wire types.</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(PairingBody))]
    [JsonSerializable(typeof(PairingReply))]
    [JsonSerializable(typeof(AmrEntry))]
    [JsonSerializable(typeof(DevicePublicKey))]
    [JsonSerializable(typeof(VerificationKeyReply))]
    [JsonSerializable(typeof(LimitsReply))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(DispatchCapabilitySnapshot))]
    [JsonSerializable(typeof(IReadOnlyList<DispatchCapabilitySnapshot>))]
    [JsonSerializable(typeof(IReadOnlyList<VerificationKeyReply>))]
    internal sealed partial class DispatchApiJsonContext : JsonSerializerContext
    {
    }
}
