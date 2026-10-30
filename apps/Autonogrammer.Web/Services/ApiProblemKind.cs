namespace Autonogrammer.Web.Services;

/// <summary>The class of failure an API problem response stands for. Each kind maps to one HTTP status the API documents.</summary>
internal enum ApiProblemKind
{
    /// <summary>400: the request was malformed or a value was invalid.</summary>
    BadRequest,

    /// <summary>401: the request carried no valid credential.</summary>
    Unauthorized,

    /// <summary>403: the caller may not do this.</summary>
    Forbidden,

    /// <summary>404: the session, option, change or route does not exist for this caller.</summary>
    NotFound,

    /// <summary>409: the request conflicts with the current state (a run in flight, a closed session, a workspace conflict).</summary>
    Conflict,

    /// <summary>413: the request body is over the configured limit.</summary>
    PayloadTooLarge,

    /// <summary>415: the request is not <c>application/json</c>.</summary>
    UnsupportedMediaType,

    /// <summary>422: the request is well formed but cannot be applied (for example a path outside the workspace).</summary>
    Unprocessable,

    /// <summary>429: the caller is rate limited.</summary>
    RateLimited,

    /// <summary>5xx: the API failed.</summary>
    Server,

    /// <summary>Any other non-success status.</summary>
    Other,
}
