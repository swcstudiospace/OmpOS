using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Autonogrammer.AgentWorker.Security;

/// <summary>
/// Configures the JwtBearer scheme with the worker's token validation parameters. The scheme reads the token from the
/// <c>Authorization</c> header only: a token in a query string would end up in access logs and is never read.
/// </summary>
internal sealed class WorkerJwtBearerSetup : IPostConfigureOptions<JwtBearerOptions>
{
    private readonly WorkerJwtValidator _validator;

    /// <summary>Initializes a new instance of the <see cref="WorkerJwtBearerSetup"/> class.</summary>
    /// <param name="validator">The worker token validator.</param>
    public WorkerJwtBearerSetup(WorkerJwtValidator validator)
    {
        _validator = validator;
    }

    /// <inheritdoc />
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.TokenValidationParameters = _validator.Parameters;
        options.MapInboundClaims = false;
        options.SaveToken = false;
    }
}
