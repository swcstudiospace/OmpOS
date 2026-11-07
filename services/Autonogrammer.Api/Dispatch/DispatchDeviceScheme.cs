using System.Security.Claims;
using System.Text.Encodings.Web;
using Autonogrammer.Api.Dispatch.Devices;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Dispatch;

/// <summary>Names of the device-credential scheme and the policy that requires it.</summary>
internal static class DispatchDeviceScheme
{
    /// <summary>Gets the authentication scheme name.</summary>
    public const string Name = "DispatchDevice";

    /// <summary>Gets the authorization policy that accepts only <see cref="Name"/>.</summary>
    public const string PolicyName = "DispatchDevicePolicy";

    /// <summary>Gets the credential audience.</summary>
    public const string Audience = "autonogrammer-dispatch-device";
    /// <summary>
    /// Authenticates the connect route from the Authorization header only. An access token fails here. The scheme is added with the
    /// parameterless <c>AddAuthentication().AddScheme</c> so the API policy-scheme selector is not edited.
    /// </summary>
    internal sealed class DispatchDeviceAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IOptions<DispatchOptions> _dispatch;

        /// <summary>Initializes a new instance of the <see cref="DispatchDeviceAuthenticationHandler"/> class.</summary>
        /// <param name="options">The scheme options.</param>
        /// <param name="logger">The logger factory.</param>
        /// <param name="encoder">The URL encoder.</param>
        /// <param name="dispatch">The dispatch options. The validator is resolved per request, so a missing device store is an answer, not a startup failure.</param>
        public DispatchDeviceAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IOptions<DispatchOptions> dispatch)
            : base(options, logger, encoder)
        {
            _dispatch = dispatch;
        }

        /// <inheritdoc />
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Query.ContainsKey("access_token"))
            {
                return AuthenticateResult.Fail("The device credential is accepted only from the Authorization header.");
            }

            var header = Request.Headers.Authorization.ToString();
            const string prefix = "Bearer ";
            if (!header.StartsWith(prefix, StringComparison.Ordinal) || header.Length == prefix.Length)
            {
                return AuthenticateResult.NoResult();
            }

            if (!_dispatch.Value.Enabled && Context.RequestServices.GetService<IDispatchStore>() is null)
            {
                // Outside Development no device store is registered. With dispatch off the credential cannot be checked, so the request is refused
                // as unauthenticated instead of failing while the handler is built.
                return AuthenticateResult.Fail("The device credential cannot be checked while dispatch is off.");
            }

            var validator = Context.RequestServices.GetRequiredService<DeviceCredentialValidator>();
            var principal = await validator.ValidateAsync(header[prefix.Length..].Trim(), Context.RequestAborted).ConfigureAwait(false);
            if (principal is null)
            {
                return AuthenticateResult.Fail("The device credential was refused.");
            }

            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
    }

    /// <summary>Re-checks a credential <c>own</c> claim against <c>Api:Auth:AllowedSubjects</c>.</summary>
    internal static class DispatchOwnerCheck
    {
        /// <summary>Evaluates <see cref="AllowedSubjectRequirement"/> for a principal whose <c>sub</c> is the credential owner.</summary>
        /// <param name="authorization">The authorization service.</param>
        /// <param name="accountId">The <c>own</c> claim.</param>
        /// <param name="cancellationToken">Cancels the check.</param>
        /// <returns>The 403 problem when the account is not allowed; otherwise null.</returns>
        public static async Task<IResult?> RejectIfNotAllowedAsync(IAuthorizationService authorization, string accountId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(authorization);
            ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
            cancellationToken.ThrowIfCancellationRequested();
            var identity = new ClaimsIdentity([new Claim(JwtBearerSetup.SubjectClaimType, accountId)], DispatchDeviceScheme.Name);
            var result = await authorization.AuthorizeAsync(new ClaimsPrincipal(identity), resource: null, [new AllowedSubjectRequirement()]).ConfigureAwait(false);
            if (result.Succeeded)
            {
                return null;
            }

            return WebProblems.Of(StatusCodes.Status403Forbidden, "subject_not_allowed", "Forbidden.", "This account is not permitted to use this deployment.");
        }
    }
}
