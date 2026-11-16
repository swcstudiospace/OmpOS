using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Accounts;

/// <summary>Registers the account module and maps its four owner-scoped routes.</summary>
internal static class AccountsModule
{
    private const string Tag = "Account";

    /// <summary>
    /// Binds <c>Accounts</c>, registers the limiter, the Cowork feature, the store and the startup warning. Call it once, after
    /// <c>AddAutomationServices</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAccountsServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddOptions<AccountsOptions>().BindConfiguration(AccountsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        _ = services.ConfigureHttpJsonOptions(static options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AccountsJsonContext.Default));
        services.AddSingleton<IAccountFeature, IAccountFeature.Cowork>();
        AccountStoreRegistration.AddAccountStore(services);
        services.AddSingleton<AccountService>();
        services.AddHostedService<SignupHint>();
        services.AddRateLimiter(static options => options.AddPolicy<string, AccountsRateLimiterPolicy>(AccountsRateLimiterPolicy.PolicyName));
        return services;
    }

    /// <summary>Maps <c>GET /account</c>, <c>/account/sessions</c>, <c>/account/sessions/{id}</c> and <c>/account/board</c>.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var routes = endpoints.MapGroup(string.Empty).WithMetadata(AccountRouteMarker.Instance);
        routes.MapGet("/account", GetAccountAsync)
            .WithName("GetAccount")
            .WithTags(Tag)
            .WithSummary("Reads the caller's account. Email comes from the token and is never stored.")
            .RequireRateLimiting(AccountsRateLimiterPolicy.PolicyName)
            .Produces<AccountResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        routes.MapGet("/account/sessions", ListSessionsAsync)
            .WithName("GetAccountSessions")
            .WithTags(Tag)
            .WithSummary("Lists the caller's Code and Cowork sessions, each labelled with its surface.")
            .RequireRateLimiting(AccountsRateLimiterPolicy.PolicyName)
            .Produces<AccountSessionList>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        routes.MapGet("/account/sessions/{id}", GetSessionAsync)
            .WithName("GetAccountSession")
            .WithTags(Tag)
            .WithSummary("Reads one of the caller's sessions. Another owner's session reads as absent.")
            .RequireRateLimiting(AccountsRateLimiterPolicy.PolicyName)
            .Produces<AccountSessionItem>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        routes.MapGet("/account/board", GetBoardAsync)
            .WithName("GetAccountBoard")
            .WithTags(Tag)
            .WithSummary("Builds the Kanban board from the caller's Code sessions.")
            .RequireRateLimiting(AccountsRateLimiterPolicy.PolicyName)
            .Produces<Autonogrammer.Contracts.Catalog.BoardResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        return endpoints;
    }

    private static async Task<JsonHttpResult<AccountResponse>> GetAccountAsync(
        HttpContext context,
        AccountService accounts,
        Autonogrammer.Api.Security.IUserContext user,
        CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(user, context, cancellationToken).ConfigureAwait(false);
        return TypedResults.Json(account, AccountsJsonContext.Default.AccountResponse);
    }

    private static async Task<JsonHttpResult<AccountSessionList>> ListSessionsAsync(
        AccountService accounts,
        Autonogrammer.Api.Security.IUserContext user,
        CancellationToken cancellationToken)
    {
        var list = await accounts.ListSessionsAsync(user, cancellationToken).ConfigureAwait(false);
        return TypedResults.Json(list, AccountsJsonContext.Default.AccountSessionList);
    }

    private static async Task<Results<JsonHttpResult<AccountSessionItem>, ProblemHttpResult>> GetSessionAsync(
        string id,
        AccountService accounts,
        Autonogrammer.Api.Security.IUserContext user,
        CancellationToken cancellationToken)
    {
        var item = await accounts.FindSessionAsync(id, user, cancellationToken).ConfigureAwait(false);
        return item is null
            ? Autonogrammer.Api.Endpoints.SessionEndpoints.SessionNotFound(id)
            : TypedResults.Json(item, AccountsJsonContext.Default.AccountSessionItem);
    }

    private static async Task<Ok<Autonogrammer.Contracts.Catalog.BoardResponse>> GetBoardAsync(
        AccountService accounts,
        Autonogrammer.Api.Security.IUserContext user,
        CancellationToken cancellationToken)
    {
        var board = await accounts.BoardAsync(user, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(board);
    }

    /// <summary>Logs the sign-up hint gap once at start when both switches are off.</summary>
    internal sealed class SignupHint : IHostedService
    {
        private readonly IOptions<ApiAuthOptions> _auth;
        private readonly ILogger<SignupHint> _logger;

        /// <summary>Initializes a new instance of the <see cref="SignupHint"/> class.</summary>
        /// <param name="auth">The authentication options.</param>
        /// <param name="logger">The logger.</param>
        public SignupHint(IOptions<ApiAuthOptions> auth, ILogger<SignupHint> logger)
        {
            _auth = auth;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var client = _auth.Value.Client;
            if (_auth.Value.Mode == ApiAuthMode.Oidc && !client.MagicLinkEnabled && !client.SignupEmailHasCode)
            {
                AccountLogMessages.SignupEmailHasNoCode(_logger);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
