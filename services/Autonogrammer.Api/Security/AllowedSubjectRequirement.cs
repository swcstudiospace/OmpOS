using Microsoft.AspNetCore.Authorization;

namespace Autonogrammer.Api.Security;

/// <summary>Requires the authenticated caller to be permitted to use the deployment (<c>Api:Auth:AllowedSubjects</c>).</summary>
internal sealed class AllowedSubjectRequirement : IAuthorizationRequirement
{
}
