using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Autonogrammer.AgentWorker.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.AgentWorker.Security;

/// <summary>
/// Validates the bearer token the control plane mints for one session. A token is accepted only when its signature verifies against a
/// configured public key with ES256 or RS256, it names the configured issuer and the <c>autonogrammer-worker</c> audience, it carries
/// <c>exp</c> and <c>iat</c>, it is not expired or issued in the future (beyond the clock skew), and <c>exp</c> minus <c>iat</c> does
/// not exceed the maximum lifetime (15 minutes). The subject is the session id; <see cref="TryGetSessionId"/> accepts it only when it is a
/// safe path segment and, for a pinned worker, equals the pinned session. Time comes from the injected <see cref="TimeProvider"/>.
/// </summary>
internal sealed class WorkerJwtValidator : IDisposable
{
    /// <summary>Gets the claim type that carries the session id.</summary>
    public const string SubjectClaimType = "sub";

    private static readonly string[] AllowedAlgorithms = [SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.RsaSha256];

    private readonly WorkerAuthOptions _auth;
    private readonly string? _pinnedSessionId;
    private readonly TimeProvider _timeProvider;
    private readonly WorkerVerificationKeySet _keys;

    /// <summary>Initializes a new instance of the <see cref="WorkerJwtValidator"/> class.</summary>
    /// <param name="auth">The validated authentication options.</param>
    /// <param name="worker">The validated worker options (for the pinned session).</param>
    /// <param name="timeProvider">The clock used for every lifetime check.</param>
    public WorkerJwtValidator(IOptions<WorkerAuthOptions> auth, IOptions<WorkerOptions> worker, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _auth = auth.Value;
        _pinnedSessionId = string.IsNullOrWhiteSpace(worker.Value.SessionId) ? null : worker.Value.SessionId;
        _timeProvider = timeProvider;
        _keys = WorkerVerificationKeySet.Parse(_auth.PublicKeysPem);
        Parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _auth.Issuer,
            ValidateAudience = true,
            ValidAudience = WorkerAuthOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = _keys.Keys,
            ValidAlgorithms = AllowedAlgorithms,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            LifetimeValidator = (notBefore, expires, token, _) => IsLifetimeAcceptable(notBefore, expires, token),
            ClockSkew = TimeSpan.Zero,
        };
    }

    /// <summary>Gets the parameters the JwtBearer scheme validates tokens with.</summary>
    public TokenValidationParameters Parameters { get; }

    /// <summary>Reads the session id from the validated principal.</summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <param name="sessionId">The session id when the subject is acceptable.</param>
    /// <returns><see langword="true"/> when the subject is a safe session id this worker serves.</returns>
    public bool TryGetSessionId(ClaimsPrincipal principal, [NotNullWhen(true)] out string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var subject = principal.FindFirst(SubjectClaimType)?.Value;
        if (subject is null
            || !WorkerSessionIds.IsSafe(subject)
            || (_pinnedSessionId is not null && !string.Equals(subject, _pinnedSessionId, StringComparison.Ordinal)))
        {
            sessionId = null;
            return false;
        }

        sessionId = subject;
        return true;
    }

    /// <inheritdoc />
    public void Dispose() => _keys.Dispose();

    private bool IsLifetimeAcceptable(DateTime? notBefore, DateTime? expires, SecurityToken securityToken)
    {
        if (expires is not { } expiry || securityToken is not JsonWebToken token)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var skew = _auth.ClockSkew;
        var issuedAt = token.IssuedAt;
        return expiry >= now - skew
            && (notBefore is not { } start || start <= now + skew)
            && issuedAt != DateTime.MinValue
            && issuedAt <= now + skew
            && expiry - issuedAt <= _auth.MaxTokenLifetime;
    }
}
